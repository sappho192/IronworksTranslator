using System.ComponentModel;

namespace IronworksTranslator.Core
{
    public enum ClientLanguage
    {
        [Description("日本語")]
        Japanese = 0,
        [Description("English")]
        English,
        [Description("Deutsch")]
        German,
        [Description("Français")]
        French,
        [Description("한국어")]
        Korean
    }
}
