using IronworksTranslator.Models.Enums;

namespace IronworksTranslator.Models
{
    public class TranslationLanguage
    {
        public TranslationLanguage(TranslationLanguageCode code, string nameEnglish, string nameNative, string webCode)
        {
            Code = code;
            NameEnglish = nameEnglish;
            NameNative = nameNative;
            WebCode = webCode;
        }

        public TranslationLanguageCode Code { get; }
        public string NameEnglish { get; }
        public string NameNative { get; }
        public string WebCode { get; }
    }
}
