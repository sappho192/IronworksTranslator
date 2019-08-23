using System.Collections.Generic;

namespace IronworksTranslator.Core
{
    public static class LanguageCodeList
    {
        public static readonly IReadOnlyCollection<LanguageCodeModel> papagoLanguageList = new List<LanguageCodeModel>
        {
            new LanguageCodeModel("ko", "Korean", "한국어"),
            new LanguageCodeModel("ja", "Japanese", "日本語"),
            new LanguageCodeModel("en", "English", "English"),
            new LanguageCodeModel("de", "German", "Deutsch"),
            new LanguageCodeModel("fr", "French", "Français"),
            new LanguageCodeModel("ru", "Russian", "русский язык")
        }.AsReadOnly();
    }
}
