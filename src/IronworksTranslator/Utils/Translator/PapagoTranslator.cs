using IronworksTranslator.Models.Enums;
using IronworksTranslator.Utils.Translators;
using Serilog;
using HtmlAgilityPack;
using IronworksTranslator.Models.Translator;
using IronworksTranslator.Utils.Aspect;

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
        private readonly object lockObj = new();

        [TraceMethod]
        public override string Translate(string sentence, TranslationLanguageCode sourceLanguage, TranslationLanguageCode targetLanguage)
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
            //lock (lockObj)
            {
                try
                {
                    var translateTask = Task.Run(async () => await RequestTranslate(url));
                    string translated = translateTask.GetAwaiter().GetResult();
                    return translated;
                }
                catch (Exception ex)
                {
                    Log.Error(ex.Message);
                    MessageBox.Show(ex.Message);
                    return sentence;
                }
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
                Console.Error.WriteLine(e);
            }
            return translated;
        }
    }
}
