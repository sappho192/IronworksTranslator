using OpenQA.Selenium.PhantomJS;
using Sharlayan;
using Sharlayan.Models;
using Sharlayan.Models.ReadResults;
using System;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using Serilog;

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
                chatTimer = new Timer(RefreshChat, null, 0, 500);
                Log.Debug($"New RefreshChat timer with period {period}ms");
            }
            else
            {
                Application.Current.Shutdown();
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
            processes = Process.GetProcessesByName(processName);
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
                    //ProcessChatMsg(readResult.ChatLogItems[i]);
                    if (int.Parse(item.Code, System.Globalization.NumberStyles.HexNumber) < 2000) // Skips battle log
                    {
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
                driver.Url = testUrl;
                driver.Navigate();
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
