using IronworksTranslator.Models.Enums;
using IronworksTranslator.Utils.Translators;
using Serilog;
using HtmlAgilityPack;
using IronworksTranslator.Models.Translator;
using IronworksTranslator.Utils.Aspect;
using System.Diagnostics;
using System.Net;

namespace IronworksTranslator.Utils.Translator
{
    public sealed class PapagoTranslator : TranslatorBase, IDisposable
    {
        private const string SourceEditorSelector = "[data-testid=\"source-editor\"]";
        // Papago's server-rendered HTML already contains the editor; it only accepts input once
        // the client app has hydrated it into a Lexical editor.
        private const string ReadySourceEditorSelector = "[data-testid=\"source-editor\"][data-lexical-editor=\"true\"]";
        private const int PageLoadAttempts = 2;
        private static readonly TimeSpan NavigationTimeout = TimeSpan.FromSeconds(20);
        private static readonly TimeSpan EditorReadyTimeout = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan TargetClearTimeout = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan TranslationTimeout = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(100);

        private const string ReadTargetEditorScript = """
            (() => {
                const editor =
                    document.querySelector('[data-testid="target-editor"]') ??
                    document.querySelector('[role="textbox"][aria-readonly="true"][contenteditable="false"][data-lexical-editor="true"]');
                if (!editor) return '';

                const lexicalNodes = editor.querySelectorAll('[data-lexical-text="true"]');
                if (lexicalNodes.length > 0) {
                    return Array.from(lexicalNodes).map(x => x.textContent ?? '').join('').trim();
                }

                return (editor.innerText ?? '').trim();
            })()
            """;

        private const string ReadSourceEditorScript = """
            (() => {
                const editor = document.querySelector('[data-testid="source-editor"]');
                return editor ? (editor.innerText ?? '').trim() : '';
            })()
            """;

        private readonly SemaphoreSlim browserSemaphore = new(1, 1);
        private readonly Dictionary<string, PapagoPage> pagesByLanguagePair = [];
        private bool disposed;

        private readonly TranslationLanguageCode[] translationLanguages = [
            TranslationLanguageCode.Japanese, TranslationLanguageCode.English,
            TranslationLanguageCode.German, TranslationLanguageCode.French,
            TranslationLanguageCode.Korean
        ];
        public override TranslationLanguageCode[] SupportedSourceLanguages => translationLanguages;
        public override TranslationLanguageCode[] SupportedTargetLanguages => translationLanguages;

        [TraceMethod]
        public override string Translate(string sentence, TranslationLanguageCode sourceLanguage, TranslationLanguageCode targetLanguage)
        {
            // Synchronous wrapper for backward compatibility. Run on the thread pool so callers on the
            // UI thread (e.g. the re-translate menu) cannot deadlock on captured continuations.
            return Task.Run(() => TranslateAsync(sentence, sourceLanguage, targetLanguage)).GetAwaiter().GetResult();
        }

        public override async Task<string> TranslateAsync(string sentence, TranslationLanguageCode sourceLanguage, TranslationLanguageCode targetLanguage)
        {
            if (!SupportedSourceLanguages.Contains(sourceLanguage))
            {
                Log.Error("Unsupported sourceLanguage");
                return sentence;
            }
            if (!SupportedTargetLanguages.Contains(targetLanguage))
            {
                Log.Error("Unsupported targetLanguage");
                return sentence;
            }

            string? sk = GetLanguageCode(sourceLanguage);
            sk ??= "ja";
            string? tk = GetLanguageCode(targetLanguage);
            tk ??= "en";

            try
            {
                await browserSemaphore.WaitAsync().ConfigureAwait(false);
                string translated;
                try
                {
                    translated = await RequestTranslate(sentence, sk, tk).ConfigureAwait(false);
                }
                finally
                {
                    browserSemaphore.Release();
                }

                if (string.IsNullOrWhiteSpace(translated))
                {
                    Log.Warning(
                        "Papago returned empty translation. SourceLanguage: {SourceLanguage}, TargetLanguage: {TargetLanguage}",
                        sourceLanguage,
                        targetLanguage);

                    ReportTranslationFailed();
                    return sentence;
                }

                ReportTranslationSucceeded();
                Log.Debug(
                    "Papago translated. SourceLanguage: {SourceLanguage}, TargetLanguage: {TargetLanguage}, Source: {Source}, Result: {Result}",
                    sourceLanguage,
                    targetLanguage,
                    sentence,
                    translated);

                return translated;
            }
            catch (Exception ex)
            {
                Log.Error(
                    ex,
                    "Error translating with Papago. SourceLanguage: {SourceLanguage}, TargetLanguage: {TargetLanguage}",
                    sourceLanguage,
                    targetLanguage);

                ReportTranslationFailed(ex);
                return sentence;
            }
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;

            browserSemaphore.Wait();
            try
            {
                foreach (var page in pagesByLanguagePair.Values)
                {
                    page.Browser.Dispose();
                }

                pagesByLanguagePair.Clear();
            }
            finally
            {
                browserSemaphore.Release();
                browserSemaphore.Dispose();
            }
        }

        private static string? GetLanguageCode(TranslationLanguageCode sourceLanguage)
        {
            foreach (var item in TranslationLanguageList.Papago)
            {
                if (item.Code == sourceLanguage)
                {
                    return item.WebCode;
                }
            }
            return null;
        }

        private async Task<string> RequestTranslate(string sourceText, string sk, string tk)
        {
            var languagePair = $"{sk}->{tk}";
            var page = await GetReadyPageAsync(languagePair, sk, tk).ConfigureAwait(false);
            try
            {
                var translated = await TranslateOnPageAsync(page.Browser, sourceText).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(translated))
                {
                    // The page may be stuck (e.g. a dropped connection); reload it before the next request.
                    page.IsReady = false;
                }

                return translated;
            }
            catch
            {
                DiscardPage(languagePair);
                throw;
            }
        }

        /// <summary>
        /// Keeps one loaded Papago page per language pair and types each sentence into it.
        /// Passing the sentence in the URL is unreliable: Papago sometimes never copies it into the editor.
        /// </summary>
        private async Task<PapagoPage> GetReadyPageAsync(string languagePair, string sk, string tk)
        {
            if (!pagesByLanguagePair.TryGetValue(languagePair, out var page))
            {
                Log.Debug("Creating Papago browser for language pair {LanguagePair}", languagePair);
                page = new PapagoPage(await WebBrowser.CreateAsync().ConfigureAwait(false));
                pagesByLanguagePair[languagePair] = page;
            }

            if (page.IsReady)
            {
                return page;
            }

            var url = $"https://papago.naver.com/?sk={sk}&tk={tk}";
            try
            {
                for (var attempt = 1; attempt <= PageLoadAttempts; attempt++)
                {
                    await page.Browser.NavigateAsync(url, NavigationTimeout).ConfigureAwait(false);
                    if (await page.Browser.WaitForSelectorAsync(ReadySourceEditorSelector, EditorReadyTimeout).ConfigureAwait(false))
                    {
                        page.IsReady = true;
                        return page;
                    }

                    // Papago's scripts occasionally fail to load (connection reset), leaving a dead page.
                    Log.Warning(
                        "Papago editor did not become ready. LanguagePair: {LanguagePair}, Attempt: {Attempt}/{Attempts}",
                        languagePair,
                        attempt,
                        PageLoadAttempts);
                }
            }
            catch
            {
                DiscardPage(languagePair);
                throw;
            }

            DiscardPage(languagePair);
            throw new TranslatorException($"Papago page did not become ready for {languagePair}.");
        }

        private static async Task<string> TranslateOnPageAsync(WebBrowser browser, string sourceText)
        {
            // Clear first and wait until the previous result disappears, so it is never mistaken for this one.
            await browser.ReplaceEditorTextAsync(SourceEditorSelector, string.Empty).ConfigureAwait(false);
            await WaitForEmptyTargetAsync(browser).ConfigureAwait(false);

            await browser.ReplaceEditorTextAsync(SourceEditorSelector, sourceText).ConfigureAwait(false);
            var typed = await browser.EvaluateExpressionAsync(ReadSourceEditorScript).ConfigureAwait(false);
            if (!IsSameText(typed, sourceText))
            {
                Log.Warning("Papago source editor did not accept the text; retrying once.");
                await browser.ReplaceEditorTextAsync(SourceEditorSelector, sourceText).ConfigureAwait(false);
            }

            var settler = new PapagoTranslationSettler(sourceText);
            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed < TranslationTimeout)
            {
                var rendered = await browser.EvaluateExpressionAsync(ReadTargetEditorScript).ConfigureAwait(false);
                var settled = settler.Observe(rendered, stopwatch.Elapsed);
                if (settled != null)
                {
                    return settled;
                }

                await Task.Delay(PollInterval).ConfigureAwait(false);
            }

            var bestEffort = settler.GetBestEffortResult();
            if (!string.IsNullOrWhiteSpace(bestEffort))
            {
                Log.Warning("Papago result did not settle before timeout; using the last rendered text.");
                return bestEffort;
            }

            var fromHtml = ExtractPapagoTargetText(await browser.GetContentAsync().ConfigureAwait(false));
            if (IsSameText(fromHtml, sourceText))
            {
                Log.Warning("Papago HTML parsing returned the original source text.");
                return string.Empty;
            }

            if (string.IsNullOrWhiteSpace(fromHtml))
            {
                Log.Warning("Papago translation result was empty after DOM polling and HTML parsing.");
            }

            return fromHtml;
        }

        private static async Task WaitForEmptyTargetAsync(WebBrowser browser)
        {
            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed < TargetClearTimeout)
            {
                var rendered = await browser.EvaluateExpressionAsync(ReadTargetEditorScript).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(rendered))
                {
                    return;
                }

                await Task.Delay(PollInterval).ConfigureAwait(false);
            }

            Log.Debug("Papago target editor was not cleared within {Timeout}.", TargetClearTimeout);
        }

        private void DiscardPage(string languagePair)
        {
            if (pagesByLanguagePair.Remove(languagePair, out var page))
            {
                page.Browser.Dispose();
            }
        }

        internal static string ExtractPapagoTargetText(string html)
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var targetEditor =
                doc.DocumentNode.SelectSingleNode("//*[@data-testid='target-editor']")
                ?? doc.DocumentNode.SelectSingleNode(
                    "//*[@role='textbox' and @aria-readonly='true' and @contenteditable='false' and @data-lexical-editor='true']");

            if (targetEditor is null)
            {
                return string.Empty;
            }

            var lexicalTextNodes = targetEditor.SelectNodes(".//*[@data-lexical-text='true']");

            string rawText = lexicalTextNodes is { Count: > 0 }
                ? string.Concat(lexicalTextNodes.Select(node => node.InnerText))
                : targetEditor.InnerText;

            return WebUtility.HtmlDecode(rawText).Trim();
        }

        internal static bool IsSameText(string? left, string? right)
        {
            return string.Equals(
                NormalizeForComparison(left),
                NormalizeForComparison(right),
                StringComparison.Ordinal);
        }

        private static string NormalizeForComparison(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : string.Concat(value.Where(ch => !char.IsWhiteSpace(ch)));
        }

        private sealed class PapagoPage(WebBrowser browser)
        {
            public WebBrowser Browser { get; } = browser;
            public bool IsReady { get; set; }
        }
    }
}
