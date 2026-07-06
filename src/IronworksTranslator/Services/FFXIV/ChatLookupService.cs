using IronworksTranslator.Models;
using IronworksTranslator.Models.Enums;
using IronworksTranslator.Utils;
using IronworksTranslator.Utils.Aspect;
using IronworksTranslator.ViewModels.Pages;
using Microsoft.Extensions.Hosting;
using Serilog;
using Sharlayan;
using Sharlayan.Enums;
using Sharlayan.Models;
using Sharlayan.Models.ReadResults;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace IronworksTranslator.Services.FFXIV
{
    public class ChatLookupService : IHostedService, IDisposable
    {
        public bool Attached { get; private set; }
        public static MemoryHandler? CurrentMemoryHandler { get; private set; }
        public int GameProcessID { get; private set; }

        private Timer? chatTimer;
        private Timer? dialogueTimer;
        // Chat polling interval: 250ms provides good responsiveness without excessive CPU usage
        private const int period = 250;
        // Dialogue polling interval: 200ms for faster dialogue detection
        private const int dPeriod = 200;

        // For chatlog you must locally store previous array offsets and indexes in order to pull the correct log from the last time you read it.
        private static int _previousArrayIndex = 0;
        private static int _previousOffset = 0;

        private readonly object _timerLock = new();
        private bool _hostStarted;
        private bool _chatLogUnavailableLogged;
        private bool _dialogueAddressUnavailableLogged;

        private string lastMessage = "";

        public ChatLookupService()
        {
            AttachGame();
            App.GetService<DashboardViewModel>().IsTranslatorActive = Attached;
            App.GetService<DashboardViewModel>().InitTranslatorToggle();
        }

        public void Initialize()
        {
            Destruct();
            AttachGame();
            if (Attached)
            {
                StartAsync(CancellationToken.None);
            }
            else
            {
                App.GetService<DashboardViewModel>().IsTranslatorActive = Attached;
                App.GetService<DashboardViewModel>().InitTranslatorToggle();
            }
        }

        public void Destruct()
        {
            if (Attached)
            {
                StopAsync(CancellationToken.None);
                DetachMemoryHandlerEvents();
                CurrentMemoryHandler?.Dispose();
                Attached = false;
                App.GetService<DashboardViewModel>().IsTranslatorActive = Attached;
                App.GetService<DashboardViewModel>().InitTranslatorToggle();
            }
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            DetachMemoryHandlerEvents();
            chatTimer?.Dispose();
            dialogueTimer?.Dispose();
        }

        public Task StartAsync(CancellationToken stoppingToken)
        {
            if (Attached)
            {
                lock (_timerLock)
                {
                    _hostStarted = true;
                    EnsureDialogueTimerStarted();
                    EnsureChatTimerStartedIfReady();
                }
            }

            return Task.CompletedTask;
        }

#pragma warning disable CS8602
        private void UpdateChat(object? state)
        {
            try
            {
                var handler = CurrentMemoryHandler;
                if (handler?.Reader.CanGetChatLog() != true)
                {
                    LogChatLogNotReady();
                    return;
                }

                ChatLogResult readResult = handler.Reader.GetChatLog(_previousArrayIndex, _previousOffset);
                _previousArrayIndex = readResult.PreviousArrayIndex;
                _previousOffset = readResult.PreviousOffset;
                if (!readResult.ChatLogItems.IsEmpty)
                {
                    foreach (var item in readResult.ChatLogItems)
                    {
                        if (!int.TryParse(item.Code, System.Globalization.NumberStyles.HexNumber, null, out var intCode))
                        {
                            Log.Warning("Failed to parse chat code: {Code}", item.Code);
                            continue;
                        }

                        ChatCode code = (ChatCode)intCode;
                        //ProcessChatMsg(readResult.ChatLogItems[i]);
                        if ((int)code < 0x9F || code == ChatCode.BossQuotes) // Skips battle log except bossquotes
                        {
                            Log.Information($"Adding {item.Message}");
                            // TryAdd with timeout to prevent blocking if queue is full
                            if (!ChatQueue.q.TryAdd(item, 100))
                            {
                                Log.Warning("Chat queue is full, dropping message: {Message}", item.Message);
                            }
                            Log.Information("Enqueue ended");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in UpdateChat timer callback");
            }
        }

        private void UpdateDialogue(object? state)
        {
            try
            {
                var raw = GetDialogueMessage();
                if (string.IsNullOrEmpty(raw))
                {
                    return;
                }

                if (ChatQueue.EnqueueDialogueIfNew(raw))
                {
                    Log.Debug("Enqueue new message: {@message}", raw);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in UpdateDialogue timer callback");
            }
        }

        private string GetDialogueMessage()
        {
            try
            {
                var handler = CurrentMemoryHandler;
                if (handler == null)
                {
                    return "";
                }

                if (!handler.Scanner.Locations.ContainsKey("ALLMESSAGES"))
                {
                    LogDialogueAddressNotReady();
                    return "";
                }

                _dialogueAddressUnavailableLogged = false;
                var message = handler.GetString(handler.Scanner.Locations["ALLMESSAGES"], 0, 2048);
                if (message != lastMessage)
                {
                    lastMessage = message;
                    return message;
                }
            }
            catch (System.ComponentModel.Win32Exception)
            {
                //MessageBox.Show(Localizer.GetString("app.exception.process.lost"));
                Log.Information("Process lost");
                Destruct();
            }
            return "";
        }

        [TraceMethod]
        public void AttachGame()
        {
            string processName = "ffxiv_dx11";
            AppPaths.MigrateLegacySharlayanCache();

            // ko client filtering
            var processes = Process.GetProcessesByName(processName).Where(x => { try { return System.IO.File.Exists(x.MainModule.FileName.Replace("game\\ffxiv_dx11.exe", "boot\\ffxivboot.exe")); } catch { return false; } }).ToArray();

            if (processes.Length > 0)
            {

                // supported: English, Chinese, Japanese, French, German, Korean
                GameRegion gameRegion = GameRegion.Global;
                GameLanguage gameLanguage = GameLanguage.English;
                // whether to always hit API on start to get the latest sigs based on patchVersion, or use the local json cache (if the file doesn't exist, API will be hit)
                bool useLocalCache = true;
                // patchVersion of game, or latest
                string patchVersion = "latest";
                Process process = processes[0];
                ProcessModel processModel = new()
                {
                    Process = process
                };
                GameProcessID = process.Id;

                var configuration = new SharlayanConfiguration
                {
                    ProcessModel = processModel,
                    GameLanguage = gameLanguage,
                    GameRegion = gameRegion,
                    PatchVersion = patchVersion,
                    UseLocalCache = useLocalCache,
                    JSONCacheDirectory = AppPaths.SharlayanCacheDirectory
                };
                _previousArrayIndex = 0;
                _previousOffset = 0;
                _chatLogUnavailableLogged = false;
                _dialogueAddressUnavailableLogged = false;

                CurrentMemoryHandler = SharlayanMemoryManager.Instance.AddHandler(configuration);
                CurrentMemoryHandler.OnMemoryLocationsFound += OnMemoryLocationsFound;
                var signatures = new List<Signature>();
                signatures.Add(new Signature
                {
                    Key = "ALLMESSAGES",
                    PointerPath = HermesAddress.GetLatestAddress().Address
                });
                CurrentMemoryHandler.Scanner.LoadOffsets([.. signatures]);
                ChatQueue.EnqueueDialogue("Dialogue window");

                Log.Information(
                    "Attached {ProcessName}.exe ({GameLanguage}). Sharlayan cache: {CacheDirectory}",
                    processName,
                    gameLanguage,
                    AppPaths.SharlayanCacheDirectory);
                Attached = true;
            }
            else
            {
                Log.Error("Couln't find FFXIV process.");
                Attached = false;
                MessageBox.Show(Localizer.GetString("dashboard.game.not_found"));
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            lock (_timerLock)
            {
                _hostStarted = false;
                chatTimer?.Change(Timeout.Infinite, 0);
                dialogueTimer?.Change(Timeout.Infinite, 0);
            }

            return Task.CompletedTask;
        }

        private void EnsureChatTimerStartedIfReady()
        {
            var handler = CurrentMemoryHandler;
            if (handler?.Reader.CanGetChatLog() != true)
            {
                LogChatLogNotReady();
                return;
            }

            _chatLogUnavailableLogged = false;
            if (chatTimer == null)
            {
                chatTimer = new Timer(UpdateChat, null, 0, period);
                Log.Information("Chat log polling started.");
            }
            else
            {// Resume
                chatTimer.Change(0, period);
            }
        }

        private void EnsureDialogueTimerStarted()
        {
            if (dialogueTimer == null)
            {
                dialogueTimer = new Timer(UpdateDialogue, null, 0, dPeriod);
            }
            else
            {
                dialogueTimer.Change(0, dPeriod);
            }
        }

        private void OnMemoryLocationsFound(
            object sender,
            ConcurrentDictionary<string, MemoryLocation> memoryLocations,
            long processingTime)
        {
            var hasChatLog = memoryLocations.ContainsKey(Signatures.CHATLOG_KEY);
            var hasDialogue = memoryLocations.ContainsKey("ALLMESSAGES");
            Log.Information(
                "Sharlayan memory locations resolved in {ProcessingTime} ms. CHATLOG: {HasChatLog}, ALLMESSAGES: {HasDialogue}",
                processingTime,
                hasChatLog,
                hasDialogue);

            if (!hasChatLog)
            {
                return;
            }

            lock (_timerLock)
            {
                if (_hostStarted)
                {
                    EnsureChatTimerStartedIfReady();
                }
            }
        }

        private void DetachMemoryHandlerEvents()
        {
            if (CurrentMemoryHandler != null)
            {
                CurrentMemoryHandler.OnMemoryLocationsFound -= OnMemoryLocationsFound;
            }
        }

        private void LogChatLogNotReady()
        {
            if (_chatLogUnavailableLogged)
            {
                return;
            }

            _chatLogUnavailableLogged = true;
            Log.Information("Waiting for Sharlayan CHATLOG memory location before starting chat polling.");
        }

        private void LogDialogueAddressNotReady()
        {
            if (_dialogueAddressUnavailableLogged)
            {
                return;
            }

            _dialogueAddressUnavailableLogged = true;
            Log.Information("Waiting for Sharlayan ALLMESSAGES memory location before reading dialogue.");
        }
#pragma warning restore CS8602
    }
}
