namespace IronworksTranslator.Core
{
    public class LanguageCodeModel
    {
        public LanguageCodeModel(string code, string nameEnglish, string nameNative)
        {
            Code = code;
            NameEnglish = nameEnglish;
            NameNative = nameNative;
        }

        public string Code { get; }
        public string NameEnglish { get; }
        public string NameNative { get; }
    }
}
