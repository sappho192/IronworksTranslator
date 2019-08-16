using OpenQA.Selenium.PhantomJS;
using Sharlayan;
using Sharlayan.Models;
using Sharlayan.Models.ReadResults;
using System;
using System.Diagnostics;
using System.Windows;
using System.Web;
using System.ComponentModel;
using System.Threading;

namespace IronworksTranslator.Core
{
    public class IronworksContext
    {
        /* Web stuff */
        public static PhantomJSDriver driver;

        /* FFXIV stuff */
        public bool Attached { get; }
        private static Process[] processes;
        public static BackgroundWorker chatFinder;

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

                chatFinder = new BackgroundWorker {
                    WorkerReportsProgress = true,
                    WorkerSupportsCancellation = true
                };
                chatFinder.DoWork += ChatFinder_DoWork;
                chatFinder.ProgressChanged += ChatFinder_ProgressChanged;
                chatFinder.RunWorkerAsync();
            }
        }

        private void ChatFinder_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
        }

        private void ChatFinder_DoWork(object sender, DoWorkEventArgs e)
        {
            var finder = sender as BackgroundWorker;
            while (!finder.CancellationPending)
            {
                Thread.Sleep(1000);
                UpdateChat();
                finder.ReportProgress(0);
            }
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

                Console.WriteLine("Attaching ffxiv_dx11.exe");
                MemoryHandler.Instance.SetProcess(processModel, gameLanguage, patchVersion, useLocalCache);
                MessageBox.Show("Attached");

                return true;
            }
            else
            {
                Console.WriteLine("Can't find ffxiv_dx11.exe");
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
                    ChatQueue.q.Enqueue(item);
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
            //the driver can now provide you with what you need (it will execute the script)
            //get the source of the page
            var source = driver.PageSource;
            string translated = String.Copy(sentence);
            //fully navigate the dom
            try
            {
                OpenQA.Selenium.IWebElement pathElement;
                do
                {
                    //Console.Write("!");
                    pathElement = driver.FindElementById("txtTarget");
                } while (pathElement.Text.Equals(""));
                translated = pathElement.Text;
                //Console.WriteLine($"Result: {pathElement.Text}");
            }
            catch (Exception e)
            {
                //MessageBox.Show(e.Message);
                translated = TranslateChat(sentence);
            }
            return translated;
        }
    }
}
