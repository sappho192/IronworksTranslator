using System.ComponentModel;

namespace IronworksTranslator.Models.Enums
{
    public enum TranslatorEngine
    {
        [Description("Papago")]
        Papago = 0,
        [Description("DeepL (API)")]
        DeepL_API = 1,
        [Description("MiLMMT (추천)")]
        MiLMMT = 2,
    }
}
