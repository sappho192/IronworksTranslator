using OpenQA.Selenium.PhantomJS;
using Sharlayan;
using Sharlayan.Models;
using Sharlayan.Models.ReadResults;
using System;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using Serilog;
using System.Linq;
using System.Collections.Generic;

namespace IronworksTranslator.Core
{
    public class IronworksContext
    {
        /* Web stuff */
        public static PhantomJSDriver driver;

        /* FFXIV stuff */
        public bool Attached { get; }
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
            return _instance ?? (_instance = new IronworksContext());
        }
        protected IronworksContext()
        {
            Attached = AttachGame();
            if (Attached)
            {
                Log.Information("Creating PhantomJS");
                var driverService = PhantomJSDriverService.CreateDefaultService();
                driverService.HideCommandPromptWindow = true;
                driver = new PhantomJSDriver(driverService);
                const int waitFor = 10;
                driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(waitFor);
                driver.Manage().Timeouts().AsynchronousJavaScript = TimeSpan.FromSeconds(10);
                driver.Manage().Timeouts().PageLoad = TimeSpan.FromSeconds(10);
                Log.Debug($"PhantomJS created, page load wait time: {waitFor}s");

                const int period = 500;
                chatTimer = new Timer(RefreshChat, null, 0, period);
                Log.Debug($"New RefreshChat timer with period {period}ms");
                const int rawPeriod = 100;
                rawChatTimer = new Timer(RefreshMessages, null, 0, rawPeriod);
                Log.Debug($"New RefreshMessages timer with period {rawPeriod}ms");

                // Following code will watch automatically kill chromeDriver.exe
                // WatchDogMain.exe is from my repo: https://github.com/sappho192/WatchDogDotNet
                var pid = Process.GetCurrentProcess().Id;
                ProcessStartInfo info = new ProcessStartInfo();
                info.FileName = "WatchDogMain.exe";
                info.Arguments = $"{pid}";
                info.WorkingDirectory = System.AppDomain.CurrentDomain.BaseDirectory;
                info.UseShellExecute = false;
                info.CreateNoWindow = true;
                Process watchdogProcess = Process.Start(info);
            }
            else
            {
                Application.Current.Shutdown();
            }
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
                } else
                {
                    if(!ChatQueue.lastMsg.Equals(raw))
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
                string gameLanguage = "English";
                // whether to always hit API on start to get the latest sigs based on patchVersion, or use the local json cache (if the file doesn't exist, API will be hit)
                bool useLocalCache = true;
                // patchVersion of game, or latest
                string patchVersion = "latest";
                Process process = processes[0];
                ProcessModel processModel = new ProcessModel
                {
                    Process = process,
                    IsWin64 = true
                };

                MemoryHandler.Instance.SetProcess(processModel, gameLanguage, patchVersion, useLocalCache);
                var signatures = new List<Signature>();
                // typical signature
                signatures.Add(new Signature
                {
                    Key = "ALLMESSAGES",

                    PointerPath = new List<long>
                {
                    0x01D3CCA8,
                    0x3C8L,
		            0x7B8L,
                    0x298L,
                    0x20L,
                    0xF8L,
                    0L
                }
                });
                signatures.Add(new Signature
                {
                    Key = "ALLMESSAGES2",

                    PointerPath = new List<long>
                {
                    0x01D69E68,
                    0x8L,
		            0x18L,
                    0x520L,
                    0x20L,
                    0xF8L,
                    0x0L
                }
                });

                // adding parameter scanAllMemoryRegions as true makes huge memory leak and CPU usage. Why?
                Scanner.Instance.LoadOffsets(signatures);

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
            ChatLogResult readResult = Reader.GetChatLog(_previousArrayIndex, _previousOffset);
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

        public string TranslateChat(string sentence, ClientLanguage from)
        {
            if(IronworksSettings.Instance == null)
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
            lock (driver)
            {
                //Log.Debug($"Translate URL: {testUrl}");
                Log.Debug($"Locked web browser for {sentence}");
                try
                {
                    driver.Url = testUrl;
                    driver.Navigate();
                }
                catch (Exception e)
                {
                    Log.Error($"Exception {e.Message} when translating {sentence}");
                    MessageBox.Show($"번역엔진이 예기치 않게 종료되었습니다.");
                    Application.Current.Shutdown();
                }
                //the driver can now provide you with what you need (it will execute the script)
                //get the source of the page
                //var source = driver.PageSource;
                string translated = string.Copy(sentence);
                //fully navigate the dom
                try
                {
                    OpenQA.Selenium.IWebElement pathElement;
                    do
                    {
                        pathElement = driver.FindElementById("txtTarget");
                    } while (pathElement.Text.Equals(""));
                    translated = pathElement.Text;
                    Log.Debug($"Successfully translated {sentence} -> {translated}");
                }
                catch (Exception e)
                {
                    Log.Error($"Exception {e.Message} when translating {sentence}");
                    translated = translated.Insert(0, "[원문]");
                }
                return translated;
            }
        }
    }
}
