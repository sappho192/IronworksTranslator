using IronworksTranslator.Models.Enums;
using IronworksTranslator.Utils.Translators;
using Serilog;
using HtmlAgilityPack;
using IronworksTranslator.Models.Translator;
using IronworksTranslator.Utils.Aspect;
using System.Net;

namespace IronworksTranslator.Utils.Translator
{
    public sealed class PapagoTranslator : TranslatorBase, IDisposable
    {
        private readonly SemaphoreSlim browserSemaphore = new(1, 1);
        private readonly Dictionary<string, WebBrowser> browsersByLanguagePair = [];
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
            // Synchronous wrapper for backward compatibility
            return TranslateAsync(sentence, sourceLanguage, targetLanguage).GetAwaiter().GetResult();
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
            string languagePair = $"{sk}->{tk}";
            string url = $"https://papago.naver.com/?sk={sk}&tk={tk}&st={Uri.EscapeDataString(sentence)}";

            try
            {
                await browserSemaphore.WaitAsync();
                string translated;
                try
                {
                    translated = await RequestTranslate(url, sentence, languagePair);
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

                    return sentence;
                }

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
                foreach (var browser in browsersByLanguagePair.Values)
                {
                    browser.Dispose();
                }

                browsersByLanguagePair.Clear();
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

        private async Task<string> RequestTranslate(string url, string sourceText, string languagePair)
        {
            var browser = GetBrowser(languagePair);
            var content = browser.Navigate(url);

            var translated = await WaitForRenderedTranslationAsync(
                browser,
                sourceText,
                timeout: TimeSpan.FromSeconds(10),
                interval: TimeSpan.FromMilliseconds(300));
            if (!string.IsNullOrWhiteSpace(translated))
            {
                return translated;
            }

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

        private WebBrowser GetBrowser(string languagePair)
        {
            if (browsersByLanguagePair.TryGetValue(languagePair, out var browser))
            {
                return browser;
            }

            Log.Debug("Creating Papago browser for language pair {LanguagePair}", languagePair);

            browser = new WebBrowser();
            browsersByLanguagePair[languagePair] = browser;
            return browser;
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

        private static async Task<string> WaitForRenderedTranslationAsync(
            WebBrowser browser,
            string sourceText,
            TimeSpan timeout,
            TimeSpan interval)
        {
            var startedAt = DateTime.UtcNow;

            while (DateTime.UtcNow - startedAt < timeout)
            {
                var translated = browser.EvaluateExpression("""
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
                    """);

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

        private static bool IsSameText(string? left, string? right)
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
    }
}
