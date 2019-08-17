using OpenQA.Selenium.PhantomJS;
using Sharlayan;
using Sharlayan.Models;
using Sharlayan.Models.ReadResults;
using System;
using System.Diagnostics;
using System.Threading;
using System.Web;
using System.Windows;

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
                var driverService = PhantomJSDriverService.CreateDefaultService();
                driverService.HideCommandPromptWindow = true;
                driver = new PhantomJSDriver(driverService);

                chatTimer = new Timer(RefreshChat, null, 0, 1000);
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
            processes = Process.GetProcessesByName("ffxiv_dx11");
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

                //Console.WriteLine("Attaching ffxiv_dx11.exe");
                MemoryHandler.Instance.SetProcess(processModel, gameLanguage, patchVersion, useLocalCache);
                MessageBox.Show("Attached ffxiv_dx11.exe");

                return true;
            }
            else
            {
                MessageBox.Show("Can't find ffxiv_dx11.exe");
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
                        ChatQueue.q.Enqueue(item);
                    }
                }
                return true;
            }
            return false;
        }

        public string TranslateChat(string sentence)
        {
            string testUrl = "https://papago.naver.com/?sk=ja&tk=ko&st=" + HttpUtility.UrlEncode(sentence);
            driver.Url = testUrl;
            driver.Navigate();
            driver.Manage().Timeouts().ImplicitlyWait(TimeSpan.FromSeconds(10));
            //the driver can now provide you with what you need (it will execute the script)
            //get the source of the page
            var source = driver.PageSource;
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
            }
            catch (Exception e)
            {
                //MessageBox.Show(e.Message);
                translated = translated.Insert(0, "[원문]");
            }
            return translated;
        }
    }
}
