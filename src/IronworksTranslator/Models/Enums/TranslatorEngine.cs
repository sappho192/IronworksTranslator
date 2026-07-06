using System.ComponentModel;

namespace IronworksTranslator.Models.Enums
{
    public enum TranslatorEngine
    {
        [Description("Papago")]
        Papago = 0,
        [Description("DeepL (API)")]
        DeepL_API = 1,
        [Description("MiLLMT (추천)")]
        MiLLMT = 2,
    }
}
