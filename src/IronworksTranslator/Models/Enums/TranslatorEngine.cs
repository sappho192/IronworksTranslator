using System.ComponentModel;

namespace IronworksTranslator.Models.Enums
{
    public enum TranslatorEngine
    {
        [Description("Papago")]
        Papago = 0,
        [Description("DeepL (API)")]
        DeepL_API,
        [Description("Ironworks Ja→Ko (Beta)")]
        Ironworks_Ja_Ko,
    }
}
