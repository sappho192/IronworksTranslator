using IronworksTranslator.Models.Enums;
using IronworksTranslator.Utils.Translators;
using Serilog;
using HtmlAgilityPack;
using IronworksTranslator.Models.Translator;
using IronworksTranslator.Utils.Aspect;
using System.Net;

namespace IronworksTranslator.Utils.Translator
{
    public sealed class PapagoTranslator : TranslatorBase
    {
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
            string url = $"https://papago.naver.com/?sk={sk}&tk={tk}&st={Uri.EscapeDataString(sentence)}";

            try
            {
                string translated = await RequestTranslate(url);

                if (string.IsNullOrWhiteSpace(translated))
                {
                    Log.Warning(
                        "Papago returned empty translation. SourceLanguage: {SourceLanguage}, TargetLanguage: {TargetLanguage}",
                        sourceLanguage,
                        targetLanguage);

                    return sentence;
                }

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

        private static async Task<string> RequestTranslate(string url)
        {
            var browser = App.GetService<WebBrowser>();
            var content = browser.Navigate(url);

            var translated = ExtractPapagoTargetText(content);
            if (!string.IsNullOrWhiteSpace(translated))
            {
                return translated;
            }

            return await WaitForRenderedTranslationAsync(
                browser,
                timeout: TimeSpan.FromSeconds(10),
                interval: TimeSpan.FromMilliseconds(300));
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
            TimeSpan timeout,
            TimeSpan interval)
        {
            var startedAt = DateTime.UtcNow;

            while (DateTime.UtcNow - startedAt < timeout)
            {
                var translated = browser.EvaluateExpression("""
                    (() => {
                        const editor = document.querySelector('[data-testid="target-editor"]');
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
                    return translated.Trim();
                }

                await Task.Delay(interval);
            }

            return string.Empty;
        }
    }
}
