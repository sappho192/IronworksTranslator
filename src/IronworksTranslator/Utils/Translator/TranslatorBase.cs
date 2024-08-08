using IronworksTranslator.Models.Enums;

namespace IronworksTranslator.Utils.Translators
{
    public abstract class TranslatorBase
    {
        public abstract TranslationLanguageCode[] SupportedLanguages { get; }
        public abstract string Translate(string input, TranslationLanguageCode sourceLanguage, TranslationLanguageCode targetLanguage);
    }
}
