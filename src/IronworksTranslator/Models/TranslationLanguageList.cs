namespace IronworksTranslator.Models
{
    public static class TranslationLanguageList
    {
        public static readonly IReadOnlyCollection<TranslationLanguage> Papago = new List<TranslationLanguage>
        {
            new(Enums.TranslationLanguageCode.Japanese, "Japanese", "日本語", "ja"),
            new(Enums.TranslationLanguageCode.English, "English", "English", "en"),
            new(Enums.TranslationLanguageCode.German, "German", "Deutsch", "de"),
            new(Enums.TranslationLanguageCode.French, "French", "Français", "fr"),
            new(Enums.TranslationLanguageCode.Korean, "Korean", "한국어", "ko"),
        }.AsReadOnly();

        public static readonly IReadOnlyCollection<TranslationLanguage> DeepL = new List<TranslationLanguage>
        {
            new(Enums.TranslationLanguageCode.Japanese, "Japanese", "日本語", "ja"),
            new(Enums.TranslationLanguageCode.English, "English", "English", "en"),
            new(Enums.TranslationLanguageCode.German, "German", "Deutsch", "de"),
            new(Enums.TranslationLanguageCode.French, "French", "Français", "fr"),
            new(Enums.TranslationLanguageCode.Korean, "Korean", "한국어", "ko"),
        }.AsReadOnly();
    }
}
