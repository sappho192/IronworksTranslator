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
using Sharlayan.Models.Resources;
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
        private static readonly Uri HermesV2LatestUri = new("https://hermes.sapphosound.com/v2/latest.json");

        // For chatlog you must locally store previous array offsets and indexes in order to pull the correct log from the last time you read it.
        private static int _previousArrayIndex = 0;
        private static int _previousOffset = 0;

        private readonly object _timerLock = new();
        private readonly TalkObservationTracker _talkObservationTracker = new();
        private bool _hostStarted;
        private bool _chatLogUnavailableLogged;
        private bool _talkUnavailableLogged;
        private bool _resourceDiagnosticsLogged;

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
            _talkObservationTracker.Reset();
            if (Attached)
            {
                StopAsync(CancellationToken.None);
                DetachMemoryHandlerEvents();
                var handler = CurrentMemoryHandler;
                if (GameProcessID <= 0 || !SharlayanMemoryManager.Instance.RemoveHandler(GameProcessID))
                {
                    handler?.Dispose();
                }

                CurrentMemoryHandler = null;
                GameProcessID = 0;
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
                    EnsureDialogueTimerStartedIfReady();
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
                var handler = CurrentMemoryHandler;
                if (handler?.Reader.CanGetTalk() != true)
                {
                    LogTalkNotReady();
                    return;
                }

                _talkUnavailableLogged = false;
                TalkResult talk = handler.Reader.GetTalk();
                if (!_talkObservationTracker.ShouldEnqueue(talk))
                {
                    return;
                }

                var dialogueEntry = new DialogueEntry(talk.Name, talk.Text);
                ChatQueue.EnqueueDialogue(dialogueEntry);
                Log.Debug(
                    "Enqueued Talk observation. Source: {TalkSource}, Visible: {IsVisible}, " +
                    "Speaker length: {SpeakerLength}, Text length: {TextLength}",
                    talk.Source,
                    talk.IsVisible,
                    dialogueEntry.Speaker.Length,
                    dialogueEntry.Text.Length);
            }
            catch (System.ComponentModel.Win32Exception)
            {
                Log.Information("Process lost");
                Destruct();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in UpdateDialogue timer callback");
            }
        }

        [TraceMethod]
        public void AttachGame()
        {
            string processName = "ffxiv_dx11";

            // ko client filtering
            var processes = Process.GetProcessesByName(processName).Where(x => { try { return System.IO.File.Exists(x.MainModule.FileName.Replace("game\\ffxiv_dx11.exe", "boot\\ffxivboot.exe")); } catch { return false; } }).ToArray();

            if (processes.Length > 0)
            {

                GameLanguage gameLanguage = GameLanguage.English;
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
                    ResourceMode = ResourceMode.RemotePreferred,
                    HermesV2LatestUri = HermesV2LatestUri,
                    ResourceCacheDirectory = AppPaths.SharlayanCacheDirectory
                };
                _previousArrayIndex = 0;
                _previousOffset = 0;
                _chatLogUnavailableLogged = false;
                _talkUnavailableLogged = false;
                _resourceDiagnosticsLogged = false;
                _talkObservationTracker.Reset();

                CurrentMemoryHandler = SharlayanMemoryManager.Instance.AddHandler(configuration);
                CurrentMemoryHandler.OnMemoryLocationsFound += OnMemoryLocationsFound;
                _ = ObserveResourceInitializationAsync(CurrentMemoryHandler);

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

        private void EnsureDialogueTimerStartedIfReady()
        {
            var handler = CurrentMemoryHandler;
            if (handler?.Reader.CanGetTalk() != true)
            {
                LogTalkNotReady();
                return;
            }

            _talkUnavailableLogged = false;
            if (dialogueTimer == null)
            {
                dialogueTimer = new Timer(UpdateDialogue, null, 0, dPeriod);
                Log.Information("Talk polling started.");
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
            if (sender is not MemoryHandler handler || !ReferenceEquals(handler, CurrentMemoryHandler))
            {
                return;
            }

            LogResourceDiagnosticsOnce(handler);
            var hasChatLog = memoryLocations.ContainsKey(Signatures.CHATLOG_KEY);
            var hasTalk = handler.Reader.CanGetTalk();
            Log.Information(
                "Sharlayan memory locations resolved in {ProcessingTime} ms. CHATLOG: {HasChatLog}, Talk: {HasTalk}",
                processingTime,
                hasChatLog,
                hasTalk);

            lock (_timerLock)
            {
                if (_hostStarted)
                {
                    EnsureChatTimerStartedIfReady();
                    EnsureDialogueTimerStartedIfReady();
                }
            }
        }

        private async Task ObserveResourceInitializationAsync(MemoryHandler handler)
        {
            try
            {
                await handler.InitializationTask.ConfigureAwait(false);
                if (ReferenceEquals(handler, CurrentMemoryHandler))
                {
                    LogResourceDiagnosticsOnce(handler);
                    lock (_timerLock)
                    {
                        if (_hostStarted)
                        {
                            EnsureChatTimerStartedIfReady();
                            EnsureDialogueTimerStartedIfReady();
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Log.Debug("Sharlayan Hermes v2 resource initialization was canceled.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Sharlayan Hermes v2 resource initialization failed.");
            }
        }

        private void LogResourceDiagnosticsOnce(MemoryHandler handler)
        {
            ResourceInfo? resourceInfo = handler.ResourceInfo;
            if (resourceInfo == null)
            {
                return;
            }

            lock (_timerLock)
            {
                if (_resourceDiagnosticsLogged)
                {
                    return;
                }

                _resourceDiagnosticsLogged = true;
            }

            Log.Information(
                "Sharlayan Hermes v2 resource selected. Source: {ResourceSource}, Revision: {ResourceRevision}, " +
                "FCS commit: {FcsCommit}, Generator commit: {GeneratorCommit}, Validation: {ValidationStatus}, " +
                "Resolved locations: {ResolvedLocationCount}, Fallback: {FallbackReason}",
                resourceInfo.Source,
                resourceInfo.ResourceRevision,
                resourceInfo.FcsCommit,
                resourceInfo.GeneratorCommit,
                resourceInfo.ValidationStatus,
                resourceInfo.ResolvedLocationCount,
                resourceInfo.FallbackReason);
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

        private void LogTalkNotReady()
        {
            if (_talkUnavailableLogged)
            {
                return;
            }

            _talkUnavailableLogged = true;
            Log.Information("Waiting for Sharlayan Talk memory locations before starting dialogue polling.");
        }
#pragma warning restore CS8602
    }
}
