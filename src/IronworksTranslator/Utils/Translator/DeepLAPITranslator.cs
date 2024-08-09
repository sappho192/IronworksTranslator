using IronworksTranslator.Models;
using IronworksTranslator.Models.Enums;
using IronworksTranslator.Utils.Translators;
using DeepL;
using DeepL.Model;
using IronworksTranslator.Models.Settings;
using Serilog;
using Lepo.i18n;
using System.Security.Policy;

namespace IronworksTranslator.Utils.Translator
{
    public sealed class DeepLAPITranslator : TranslatorBase
    {
        private readonly TranslationLanguageCode[] translationLanguages = [
            TranslationLanguageCode.Japanese, TranslationLanguageCode.English,
            TranslationLanguageCode.German, TranslationLanguageCode.French,
            TranslationLanguageCode.Korean
        ];
        public override TranslationLanguageCode[] SupportedLanguages => translationLanguages;

        private DeepL.Translator? translator;

        public override string Translate(string input, TranslationLanguageCode sourceLanguage, TranslationLanguageCode targetLanguage)
        {
            if (translator == null)
            {
                InitTranslator(testApi: true);
            }

            if (!SupportedLanguages.Contains(sourceLanguage))
            {
                throw new TranslatorException("Unsupported sourceLanguage");
            }
            if (!SupportedLanguages.Contains(targetLanguage))
            {
                throw new TranslatorException("Unsupported targetLanguage");
            }

            var translateTask = Task.Run(async () => await RequestTranslate(input, sourceLanguage, targetLanguage));
            string translated = translateTask.GetAwaiter().GetResult();
            return translated;
        }

#pragma warning disable CS8602, CS8604
        private async Task<string> RequestTranslate(string input, 
            TranslationLanguageCode sourceLanguage, TranslationLanguageCode targetLanguage)
        {
            try
            {
                var translated = translator.TranslateTextAsync(
                    text: input,
                    sourceLanguageCode: GetLanguageCode(sourceLanguage),
                    targetLanguageCode: GetLanguageCode(targetLanguage)
                );
                return translated.Result.Text;
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message);
                return string.Empty;
            }
        }
#pragma warning restore CS8602, CS8604

#pragma warning disable CS8602, CS8604
        public void InitTranslator(bool testApi = false)
        {
            var apiKey = IronworksSettings.Instance.TranslatorSettings.DeeplApiKey;
            var options = new TranslatorOptions
            {
                appInfo = new AppInfo
                {
                    AppName = "IronworksTranslator",
                    AppVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0"
                }
            };
            try
            {
                translator = new DeepL.Translator(apiKey, options);
            }
            catch (ArgumentException ex)
            {
                Log.Error(ex.Message);
                translator = null;
                return;
            }

            if (testApi)
            {
                TestTranslator();
            }
        }
#pragma warning restore CS8602, CS8604

        private async void TestTranslator()
        {
            if (translator == null) return;
            try
            {
                var usage = await translator.GetUsageAsync();
                if (usage.AnyLimitReached)
                {
                    MessageBox.Show("DeepL API limit reached. Please try again later.");
                }
#pragma warning disable CS8602
                var capacity = usage.Character.Limit;
#pragma warning restore CS8602
                var used = usage.Character.Count;
                Log.Information("DeepL API usage: {Used}/{Capacity}", used, capacity);
            }
            catch (AuthorizationException ex)
            {
                Log.Error(ex.Message);
                MessageBox.Show(ex.Message);
            }
        }

        private static string? GetLanguageCode(TranslationLanguageCode sourceLanguage)
        {
            foreach (var item in TranslationLanguageList.DeepL)
            {
                if (item.Code == sourceLanguage)
                {
                    return item.WebCode;
                }
            }
            return null;
        }
    }
}
