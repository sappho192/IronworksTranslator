using IronworksTranslator.Models.Enums;
using IronworksTranslator.Utils.Translators;
using DeepL;
using IronworksTranslator.Models.Settings;
using Serilog;
using IronworksTranslator.Models.Translator;
using IronworksTranslator.Utils.Aspect;

namespace IronworksTranslator.Utils.Translator
{
    public sealed class DeepLAPITranslator : TranslatorBase
    {
        private readonly TranslationLanguageCode[] translationLanguages = [
            TranslationLanguageCode.Japanese, TranslationLanguageCode.English,
            TranslationLanguageCode.German, TranslationLanguageCode.French,
            TranslationLanguageCode.Korean
        ];
        public override TranslationLanguageCode[] SupportedSourceLanguages => translationLanguages;
        public override TranslationLanguageCode[] SupportedTargetLanguages => translationLanguages;

        private DeepL.Translator? translator;

        private int currentApiKeyIndex = 0;

        public override string Translate(string input, TranslationLanguageCode sourceLanguage, TranslationLanguageCode targetLanguage)
        {
            if (translator == null)
            {
                if (!InitTranslator(testApi: true))
                {
                    return input;
                }
            }

            if (!SupportedSourceLanguages.Contains(sourceLanguage))
            {
                Log.Error("Unsupported sourceLanguage");
                return input;
            }
            if (!SupportedTargetLanguages.Contains(targetLanguage))
            {
                Log.Error("Unsupported targetLanguage");
                return input;
            }

            var translateTask = Task.Run(async () => await RequestTranslate(input, sourceLanguage, targetLanguage));
            string translated = translateTask.GetAwaiter().GetResult();
            return translated;
        }

#pragma warning disable CS8602, CS8604
        private async Task<string> RequestTranslate(string input,
            TranslationLanguageCode sourceLanguage, TranslationLanguageCode targetLanguage)
        {
            /*
             * Since initialization task should be completed before this method is called,
             * the only situation where translator is null is when there're no API key available.
             * So just return the original text.
             */
            if (translator == null)
            {
                return input;
            }

            try
            {
                var translated = translator.TranslateTextAsync(
                    text: input,
                    sourceLanguageCode:
                        IronworksSettings.Instance.TranslatorSettings.DeeplAutoSourceLanguage
                            ? null : GetLanguageCode(sourceLanguage),
                    targetLanguageCode: GetLanguageCode(targetLanguage)
                );
                return translated.Result.Text;
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message);
                return input;
            }
        }
#pragma warning restore CS8602, CS8604

#pragma warning disable CS8602, CS8604
        [TraceMethod]
        public bool InitTranslator(bool testApi = false)
        {
            if (IronworksSettings.Instance.TranslatorSettings.DeeplApiKeys.Count == 0)
            {
                MessageBox.Show(Localizer.GetString("settings.translator.engine.deepl_api.not_exists"));
                return false;
            }

            for (; currentApiKeyIndex < IronworksSettings.Instance.TranslatorSettings.DeeplApiKeys.Count; currentApiKeyIndex++)
            {
                var apiKey = IronworksSettings.Instance.TranslatorSettings.DeeplApiKeys[currentApiKeyIndex];
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
                    string message = $"API Key: {apiKey}, {ex.Message}";
                    Log.Error(message);
                    MessageBox.Show(message);
                    translator = null;
                    continue;
                }

                if (testApi)
                {
                    if (Task.Run(TestTranslator).GetAwaiter().GetResult())
                    {
                        return true;
                    }
                    else
                    {
                        translator = null;
                        continue;
                    }
                }
            }
            translator = null;
            return false;
        }

        [TraceMethod]
        private async Task<bool> TestTranslator()
        {
            if (translator == null) return false;
            var apiKey = IronworksSettings.Instance.TranslatorSettings.DeeplApiKeys[currentApiKeyIndex];
            try
            {
                var usage = await translator.GetUsageAsync();
                if (usage.AnyLimitReached)
                {
                    string message = $"{Localizer.GetString("settings.translator.engine.deepl_api.limit_reached")}: {apiKey}";
                    MessageBox.Show(message);
                    return false;
                }
                var capacity = usage.Character.Limit;
                var used = usage.Character.Count;
                Log.Information("DeepL API usage: {Used}/{Capacity}", used, capacity);
            }
            catch (AuthorizationException ex)
            {
                string message = $"API Key: {apiKey}, {ex.Message}";
                Log.Error(message);
                MessageBox.Show(message);
                return false;
            }
            return true;
        }
#pragma warning restore CS8602, CS8604

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
