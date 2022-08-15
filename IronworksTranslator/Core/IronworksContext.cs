using Sharlayan;
using Sharlayan.Models;
using Sharlayan.Models.ReadResults;
using System;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using Serilog;
using System.Collections.Generic;
using IronworksTranslator.Util;
using Sharlayan.Enums;
using HtmlAgilityPack;
using PuppeteerSharp;
using System.Threading.Tasks;
using System.Linq;

namespace IronworksTranslator.Core
{
    public class IronworksContext
    {
        /* Web stuff */
        public Browser webBrowser = null;
        private async Task<Browser> initBrowser()
        {
            await new BrowserFetcher().DownloadAsync(BrowserFetcher.DefaultChromiumRevision);
            var _browser = await Puppeteer.LaunchAsync(new LaunchOptions
            {
                Headless = true,
                Args = new[] {
                    "--js-flags=\"--max-old-space-size=128\""
                },
            });
            return _browser;
        }
        private Page webPage = null;

        /* FFXIV stuff */
        public bool Attached { get; }
        public static MemoryHandler CurrentMemoryHandler { get; set; }
        private static Process[] processes;
        private readonly Timer chatTimer;
        private readonly Timer rawChatTimer;

        // For chatlog you must locally store previous array offsets and indexes in order to pull the correct log from the last time you read it.
        private static int _previousArrayIndex = 0;
        private static int _previousOffset = 0;


        /* Singleton context */
        private static IronworksContext _instance;

        public static IronworksContext Instance()
        {// make new instance if null
            return _instance ??= new IronworksContext();
        }
        protected IronworksContext()
        {
            Attached = AttachGame();
            if (Attached)
            {
                Log.Information("Creating PhantomJS");
                InitWebBrowser();

                const int period = 500;
                chatTimer = new Timer(RefreshChat, null, 0, period);
                Log.Debug($"New RefreshChat timer with period {period}ms");
                const int rawPeriod = 100;
                rawChatTimer = new Timer(RefreshMessages, null, 0, rawPeriod);
                Log.Debug($"New RefreshMessages timer with period {rawPeriod}ms");

                // Following code will watch automatically kill chromeDriver.exe
                // WatchDogMain.exe is from my repo: https://github.com/sappho192/WatchDogDotNet
                BootWatchDog();
            }
            else
            {
                Application.Current.Shutdown();
            }
        }

        private static void BootWatchDog()
        {
            var pid = Process.GetCurrentProcess().Id;
            ProcessStartInfo info = new()
            {
                FileName = "WatchDogMain.exe",
                Arguments = $"{pid}",
                WorkingDirectory = System.AppDomain.CurrentDomain.BaseDirectory,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            Process watchdogProcess = Process.Start(info);
        }

        private void InitWebBrowser()
        {
            const int waitFor = 0;

            var browserTask = Task.Run(async () => await initBrowser());
            webBrowser = browserTask.GetAwaiter().GetResult();
            var pageTask = Task.Run(async () => await webBrowser.NewPageAsync());
            webPage = pageTask.GetAwaiter().GetResult();
            webPage.DefaultTimeout = waitFor;

            Log.Debug($"PhantomJS created, page load wait time: {waitFor}s");
        }

        private void RefreshMessages(object state)
        {
            var raw = AdvancedReader.getMessage();
            if (raw.Equals(""))
            {
                return;
            }
            lock (ChatQueue.rq)
            {
                if (ChatQueue.rq.TryPeek(out string lastMsg))
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

        private void RefreshChat(object state)
        {
            UpdateChat();
        }
        private static bool AttachGame()
        {
            string processName = "ffxiv_dx11";
            Log.Debug($"Finding process {processName}");

            // ko client filtering
            processes = Process.GetProcessesByName(processName).Where( x => { try { return System.IO.File.Exists(x.MainModule.FileName.Replace("game\\ffxiv_dx11.exe", "boot\\ffxivboot.exe")); } catch { return false; } }).ToArray();

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
                // typical signature
                signatures.Add(new Signature
                {
                    Key = "ALLMESSAGES",

                    PointerPath = HermesAddress.GetLatestAddress().Address
                    /*PointerPath = new List<long>
                {
                    0x01EB7478,
                    0x8L,
                    0x18L,
                    0x20L,
                    0x100L,
                    0x0L
                }
                });*/
                });
                //signatures.Add(new Signature
                //{
                //    Key = "ALLMESSAGES2",
                //    PointerPath = new List<long>
                //{
                //    0x01E7FD80,
                //    0x108L,
                //    0x68L,
                //    0x240L,
                //    0x0L
                //}
                //});

                // adding parameter scanAllMemoryRegions as true makes huge memory leak and CPU usage.Why?
                CurrentMemoryHandler.Scanner.LoadOffsets(signatures.ToArray());

                ChatQueue.rq.Enqueue("Dialogue window");
                ChatQueue.lastMsg = "Dialogue window";
                Log.Debug($"Attached {processName}.exe ({gameLanguage})");
                MessageBox.Show($"Attached {processName}.exe");

                return true;
            }
            else
            {
                Log.Fatal($"Can't find {processName}.exe");
                MessageBox.Show($"Can't find {processName}.exe");
                return false;
            }
        }

        public bool UpdateChat()
        {
            ChatLogResult readResult = CurrentMemoryHandler.Reader.GetChatLog(_previousArrayIndex, _previousOffset);
            _previousArrayIndex = readResult.PreviousArrayIndex;
            _previousOffset = readResult.PreviousOffset;
            if (readResult.ChatLogItems.Count > 0)
            {
                foreach (var item in readResult.ChatLogItems)
                {
                    ChatCode code = (ChatCode)int.Parse(item.Code, System.Globalization.NumberStyles.HexNumber);
                    //ProcessChatMsg(readResult.ChatLogItems[i]);
                    if ((int)code < 0x9F) // Skips battle log
                    {
                        if (code == ChatCode.GilReceive || code == ChatCode.Gather || code == ChatCode.FieldAttack || code == ChatCode.EmoteCustom) continue;
                        ChatQueue.q.Add(item);
                    }
                }
                return true;
            }
            return false;
        }

        private async Task<string> RequestTranslate(string url)
        {
            await webPage.GoToAsync(url, WaitUntilNavigation.Networkidle2);
            var content = await webPage.GetContentAsync();

            var doc = new HtmlDocument();
            doc.LoadHtml(content);
            string translated = string.Empty;
            try
            {
                var pathElement = doc.GetElementbyId("txtTarget");
                translated = pathElement.InnerText.Trim();
            }
            catch (Exception e)
            {
                Log.Error($"Exception {e.Message} when translating the sentence.");
            }
            return translated;
        }

        public string TranslateChat(string sentence, ClientLanguage from)
        {
            if (IronworksSettings.Instance == null)
            {
                throw new Exception("IronworksSettings is null");
            }
            string tk = "ko";
            foreach (var item in LanguageCodeList.papago)
            {
                if (IronworksSettings.Instance.Translator.NativeLanguage.ToString().Equals(item.NameEnglish))
                {
                    tk = item.Code;
                }
            }
            string sk = "ja";
            foreach (var item in LanguageCodeList.papago)
            {
                if (from.ToString().Equals(item.NameEnglish))
                {
                    sk = item.Code;
                }
            }
            string testUrl = $"https://papago.naver.com/?sk={sk}&tk={tk}&st={Uri.EscapeDataString(sentence)}";
            lock (webPage)
            {
                //Log.Debug($"Translate URL: {testUrl}");
                Log.Debug($"Locked web browser for {sentence}");
                string translated = sentence;
                try
                {
                    var translateTask = Task.Run(async () => await RequestTranslate(testUrl));
                    translated = translateTask.GetAwaiter().GetResult();
                }
                catch (Exception e)
                {
                    Log.Error($"Exception {e.Message} when translating {sentence}");
                    MessageBox.Show($"번역엔진이 예기치 않게 종료되었습니다.");
                    Application.Current.Shutdown();
                }

                if (translated == null)
                {
                    translated = translated.Insert(0, "[원문]");
                }
                return translated;
            }
        }
    }
}
