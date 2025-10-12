using IronworksTranslator.Models.Enums;

namespace IronworksTranslator.Utils.Translators
{
    public abstract class TranslatorBase
    {
        public abstract TranslationLanguageCode[] SupportedSourceLanguages { get; }
        public abstract TranslationLanguageCode[] SupportedTargetLanguages { get; }

        // Synchronous version for backward compatibility
        public abstract string Translate(string input, TranslationLanguageCode sourceLanguage, TranslationLanguageCode targetLanguage);

        // Asynchronous version for better performance
        public abstract Task<string> TranslateAsync(string input, TranslationLanguageCode sourceLanguage, TranslationLanguageCode targetLanguage);
    }
}
