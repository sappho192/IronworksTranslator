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
        private readonly Dictionary<string, PapagoBrowserSession> papagoBrowserSessions = new();
        private readonly object papagoBrowserLock = new();
        private readonly object papagoTranslationLock = new();
        private async Task<Browser> initBrowser()
        {
            await new BrowserFetcher().DownloadAsync(PuppeteerSharp.BrowserData.Chrome.DefaultBuildId);
            var _browser = await Puppeteer.LaunchAsync(new LaunchOptions
            {
                Headless = true,
                Args = new[] {
                    "--js-flags=\"--max-old-space-size=128\""
                },
            });
            return (Browser)_browser;
        }
        private Page webPage = null;

        private sealed class PapagoBrowserSession
        {
            public Browser Browser { get; init; }
            public Page Page { get; init; }
        }

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
            GetPapagoBrowserSession("ja->ko");
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
                MessageBox.Show($"아이언웍스 번역기를 실행합니다.");

                return true;
            }
            else
            {
                Log.Fatal($"Can't find {processName}.exe");
                MessageBox.Show($"파판을 먼저 켠 다음에 번역기를 실행해주세요.");
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

        public void DisposeWebBrowsers()
        {
            lock (papagoTranslationLock)
            {
                lock (papagoBrowserLock)
                {
                    foreach (var session in papagoBrowserSessions.Values)
                    {
                        try
                        {
                            session.Page?.CloseAsync().GetAwaiter().GetResult();
                            session.Browser?.CloseAsync().GetAwaiter().GetResult();
                        }
                        catch (Exception e)
                        {
                            Log.Warning(e, "Exception when closing Papago browser.");
                        }

                        session.Page?.Dispose();
                        session.Browser?.Dispose();
                    }

                    papagoBrowserSessions.Clear();
                    webPage = null;
                    webBrowser = null;
                }
            }
        }

        private PapagoBrowserSession GetPapagoBrowserSession(string languagePair)
        {
            lock (papagoBrowserLock)
            {
                if (papagoBrowserSessions.TryGetValue(languagePair, out var session))
                {
                    return session;
                }

                const int waitFor = 0;

                Log.Debug("Creating Papago browser session. LanguagePair: {LanguagePair}", languagePair);
                var browserTask = Task.Run(async () => await initBrowser());
                Browser browser = browserTask.GetAwaiter().GetResult();
                var pageTask = Task.Run(async () => await browser.NewPageAsync());
                Page page = (Page)pageTask.GetAwaiter().GetResult();
                page.DefaultTimeout = waitFor;

                session = new PapagoBrowserSession
                {
                    Browser = browser,
                    Page = page
                };

                papagoBrowserSessions.Add(languagePair, session);

                if (webBrowser == null)
                {
                    webBrowser = browser;
                    webPage = page;
                }

                Log.Debug("Papago browser session created. LanguagePair: {LanguagePair}, page load wait time: {WaitFor}s", languagePair, waitFor);
                return session;
            }
        }

        private async Task<string> RequestTranslate(string url, string sourceText, string languagePair)
        {
            var session = GetPapagoBrowserSession(languagePair);
            await session.Page.GoToAsync(url, WaitUntilNavigation.Networkidle2);

            string translated = await WaitForRenderedTranslationAsync(
                session.Page,
                sourceText,
                timeout: TimeSpan.FromSeconds(10),
                interval: TimeSpan.FromMilliseconds(300));

            if (!string.IsNullOrWhiteSpace(translated))
            {
                return translated;
            }

            var content = await session.Page.GetContentAsync();
            translated = ExtractPapagoTargetText(content);

            if (IsSameText(translated, sourceText))
            {
                Log.Warning("Papago HTML parsing returned the original source text.");
                return string.Empty;
            }

            if (string.IsNullOrWhiteSpace(translated))
            {
                Log.Warning("Papago translation result was empty after DOM polling and HTML parsing.");
            }

            return translated;
        }

        private async Task<string> WaitForRenderedTranslationAsync(Page page, string sourceText, TimeSpan timeout, TimeSpan interval)
        {
            var startedAt = DateTime.UtcNow;
            const string script = @"(() => {
                const editor =
                    document.querySelector('[data-testid=""target-editor""]') ??
                    document.querySelector('[role=""textbox""][aria-readonly=""true""][contenteditable=""false""][data-lexical-editor=""true""]');

                if (!editor) {
                    return '';
                }

                const lexicalNodes = editor.querySelectorAll('[data-lexical-text=""true""]');
                if (lexicalNodes.length > 0) {
                    return Array.from(lexicalNodes)
                        .map(node => node.textContent ?? '')
                        .join('')
                        .trim();
                }

                return (editor.innerText ?? '').trim();
            })()";

            while (DateTime.UtcNow - startedAt < timeout)
            {
                string translated = await page.EvaluateExpressionAsync<string>(script);
                if (!string.IsNullOrWhiteSpace(translated))
                {
                    translated = translated.Trim();
                    if (!IsSameText(translated, sourceText))
                    {
                        return translated;
                    }
                }

                await Task.Delay(interval);
            }

            return string.Empty;
        }

        private static string ExtractPapagoTargetText(string html)
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var targetEditor =
                doc.DocumentNode.SelectSingleNode("//*[@data-testid='target-editor']")
                ?? doc.DocumentNode.SelectSingleNode(
                    "//*[@role='textbox' and @aria-readonly='true' and @contenteditable='false' and @data-lexical-editor='true']");

            if (targetEditor == null)
            {
                Log.Warning("Papago target editor element was not found.");
                return string.Empty;
            }

            var lexicalTextNodes = targetEditor.SelectNodes(".//*[@data-lexical-text='true']");
            string rawText = lexicalTextNodes is { Count: > 0 }
                ? string.Concat(lexicalTextNodes.Select(node => node.InnerText))
                : targetEditor.InnerText;

            return System.Net.WebUtility.HtmlDecode(rawText).Trim();
        }

        private static bool IsSameText(string left, string right)
        {
            return string.Equals(
                NormalizeForComparison(left),
                NormalizeForComparison(right),
                StringComparison.Ordinal);
        }

        private static string NormalizeForComparison(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : string.Concat(value.Where(ch => !char.IsWhiteSpace(ch)));
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
            string languagePair = $"{sk}->{tk}";
            string testUrl = $"https://papago.naver.com/?sk={sk}&tk={tk}&st={Uri.EscapeDataString(sentence)}";
            lock (papagoTranslationLock)
            {
                //Log.Debug($"Translate URL: {testUrl}");
                Log.Debug("Locked web browser for Papago translation. SourceLanguage: {SourceLanguage}, TargetLanguage: {TargetLanguage}", sk, tk);
                string translated = sentence;
                try
                {
                    var translateTask = Task.Run(async () => await RequestTranslate(testUrl, sentence, languagePair));
                    translated = translateTask.GetAwaiter().GetResult();
                }
                catch (Exception e)
                {
                    Log.Error(e, "Exception when translating with Papago. SourceLanguage: {SourceLanguage}, TargetLanguage: {TargetLanguage}", sk, tk);
                    return sentence;
                }

                if (string.IsNullOrWhiteSpace(translated))
                {
                    Log.Warning("Papago returned empty translation. SourceLanguage: {SourceLanguage}, TargetLanguage: {TargetLanguage}", sk, tk);
                    return sentence;
                }

                return translated;
            }
        }
    }
}
