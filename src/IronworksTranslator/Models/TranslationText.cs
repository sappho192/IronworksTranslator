using IronworksTranslator.Models.Enums;

namespace IronworksTranslator.Models
{
    public class TranslationText(string originalText, TranslationLanguageCode sourceLanguage, TranslationLanguageCode targetLanguage)
    {
        public string OriginalText { get; } = originalText;
        public string? TranslatedText { get; set; }
        public TranslationLanguageCode SourceLanguage { get; } = sourceLanguage;
        public TranslationLanguageCode TargetLanguage { get; } = targetLanguage;
    }
}
