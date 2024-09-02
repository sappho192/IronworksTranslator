using IronworksTranslator.Models;
using IronworksTranslator.Models.Enums;
using IronworksTranslator.Utils;
using IronworksTranslator.ViewModels.Pages;
using Microsoft.Extensions.Hosting;
using Serilog;
using Sharlayan;
using Sharlayan.Enums;
using Sharlayan.Models;
using Sharlayan.Models.ReadResults;
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
        private const int period = 250;
        private const int dPeriod = 200;
        private object lockObj = new();

        // For chatlog you must locally store previous array offsets and indexes in order to pull the correct log from the last time you read it.
        private static int _previousArrayIndex = 0;
        private static int _previousOffset = 0;

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
                CurrentMemoryHandler?.Dispose();
                Attached = false;
            }
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            chatTimer?.Dispose();
        }

        public Task StartAsync(CancellationToken stoppingToken)
        {
            if (Attached)
            {
                if (chatTimer == null)
                {
                    chatTimer = new Timer(UpdateChat, null, 0, period);
                }
                else
                {// Resume
                    chatTimer.Change(0, period);
                }
                if (dialogueTimer == null)
                {
                    dialogueTimer = new Timer(UpdateDialogue, null, 0, dPeriod);
                }
                else
                {
                    dialogueTimer.Change(0, dPeriod);
                }
            }

            return Task.CompletedTask;
        }

#pragma warning disable CS8602
        private void UpdateChat(object? state)
        {
            ChatLogResult readResult = CurrentMemoryHandler.Reader.GetChatLog(_previousArrayIndex, _previousOffset);
            _previousArrayIndex = readResult.PreviousArrayIndex;
            _previousOffset = readResult.PreviousOffset;
            if (!readResult.ChatLogItems.IsEmpty)
            {
                foreach (var item in readResult.ChatLogItems)
                {
                    ChatCode code = (ChatCode)int.Parse(item.Code, System.Globalization.NumberStyles.HexNumber);
                    //ProcessChatMsg(readResult.ChatLogItems[i]);
                    if ((int)code < 0x9F || code == ChatCode.BossQuotes) // Skips battle log except bossquotes
                    {
                        Log.Information($"Adding {item.Message}");
                        ChatQueue.q.Add(item);
                        Log.Information("Enqueue ended");
                    }
                }
            }
        }

        private void UpdateDialogue(object? state)
        {
            var raw = GetDialogueMessage();
            if (raw.Equals(""))
            {
                return;
            }
            lock (ChatQueue.rq)
            {
                if (ChatQueue.rq.TryPeek(out string? lastMsg))
                {
                    if (!lastMsg.Equals(raw))
                    {
                        Log.Debug("Enqueue new message: {@message}", raw);
                        ChatQueue.rq.Enqueue(raw);
                        lock (ChatQueue.lastMsg)
                        {
                            ChatQueue.lastMsg = raw;
                        }
                    }
                }
                else
                {
                    if (!ChatQueue.lastMsg.Equals(raw))
                    {
                        Log.Debug("Enqueue new message: {@message}", raw);
                        ChatQueue.rq.Enqueue(raw);
                        lock (ChatQueue.lastMsg)
                        {
                            ChatQueue.lastMsg = raw;
                        }
                    }
                }
            }
        }

        private string GetDialogueMessage()
        {
            try
            {
                var handler = CurrentMemoryHandler;
                var message = handler.GetString(handler.Scanner.Locations["ALLMESSAGES"], 0, 2048);
                if (message != lastMessage)
                {
                    lastMessage = message;
                    return message;
                }
            }
            catch (System.ComponentModel.Win32Exception)
            {
                MessageBox.Show(Localizer.GetString("app.exception.process.lost"));
                Destruct();
            }
            return "";
        }

        [TraceMethod]
        public void AttachGame()
        {
            string processName = "ffxiv_dx11";

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
                    UseLocalCache = useLocalCache
                };
                CurrentMemoryHandler = SharlayanMemoryManager.Instance.AddHandler(configuration);
                var signatures = new List<Signature>();
                signatures.Add(new Signature
                {
                    Key = "ALLMESSAGES",
                    PointerPath = HermesAddress.GetLatestAddress().Address
                });
                CurrentMemoryHandler.Scanner.LoadOffsets([.. signatures]);
                ChatQueue.rq.Enqueue("Dialogue window");
                ChatQueue.lastMsg = "Dialogue window";

                Log.Information($"Attached {processName}.exe ({gameLanguage})");
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
            chatTimer?.Change(Timeout.Infinite, 0);
            return Task.CompletedTask;
        }
#pragma warning restore CS8602
    }
}
