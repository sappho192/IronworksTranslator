using IronworksTranslator.Models.Enums;

namespace IronworksTranslator.Utils.Translators
{
    public abstract class TranslatorBase
    {
        public abstract TranslationLanguageCode[] SupportedSourceLanguages { get; }
        public abstract TranslationLanguageCode[] SupportedTargetLanguages { get; }
        public abstract string Translate(string input, TranslationLanguageCode sourceLanguage, TranslationLanguageCode targetLanguage);
    }
}
