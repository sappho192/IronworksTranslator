using IronworksTranslator.Models.Enums;

namespace IronworksTranslator.Models.Translator
{
    public class TranslationText(string originalText, TranslationLanguageCode sourceLanguage, TranslationLanguageCode targetLanguage)
    {
        public string OriginalText { get; } = originalText;
        public string? TranslatedText { get; set; }
        public string? Author { get; set; }
        public TranslationLanguageCode SourceLanguage { get; } = sourceLanguage;
        public TranslationLanguageCode TargetLanguage { get; } = targetLanguage;
    }
}
