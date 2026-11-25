using IronworksTranslator.Models.Enums;
using IronworksTranslator.Utils.Translator;

namespace IronworksTranslator.Models.Translator
{
    public class TranslationText(string originalText, TranslationLanguageCode sourceLanguage, TranslationLanguageCode targetLanguage)
    {
        public string OriginalText { get; } = originalText;
        public string? TranslatedText { get; set; }
        public string? Author { get; set; }
        public string? OriginalSpeaker { get; set; }
        public TranslationLanguageCode SourceLanguage { get; } = sourceLanguage;
        public TranslationLanguageCode TargetLanguage { get; } = targetLanguage;
        public TranslatorEngine? Engine { get; set; }
        public string? ModelId { get; set; }
        public string? ModelSha256 { get; set; }
        public MiLMMTTranslationKind TranslationKind { get; set; } = MiLMMTTranslationKind.Chat;
    }
}
