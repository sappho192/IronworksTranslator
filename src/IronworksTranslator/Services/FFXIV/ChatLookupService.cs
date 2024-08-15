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
        private const int period = 500;
        private object lockObj = new();

        // For chatlog you must locally store previous array offsets and indexes in order to pull the correct log from the last time you read it.
        private static int _previousArrayIndex = 0;
        private static int _previousOffset = 0;

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
            StartAsync(CancellationToken.None);
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
            //chatTimer = new Timer(RefreshChat, null, 0, period);
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
            }

            return Task.CompletedTask;
        }

#pragma warning disable CS8602
        private void UpdateChat(object? state)
        {
            lock (lockObj)
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
                            lock (ChatQueue.oq)
                            {
                                Log.Information($"Adding {item.Message}");
                                ChatQueue.oq.Enqueue(item);
                            }
                            Log.Information("Enqueue ended");
                        }
                    }
                }
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

                Log.Information($"Attached {processName}.exe ({gameLanguage})");
                Attached = true;
            }
            else
            {
                string message = "Couln't find FFXIV process.";
                Log.Error(message);
                Attached = false;
                MessageBox.Show(message);
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
