using System.ComponentModel;

namespace IronworksTranslator.Models.Enums
{
    public enum TranslatorEngine
    {
        [Description("Papago")]
        Papago = 0,
        [Description("DeepL (API)")]
        DeepL_API,
        [Description("Ironworks Ja→Ko (사용 금지)")]
        Ironworks_Ja_Ko,
        [Description("MiLLMT (추천)")]
        MiLLMT,
    }
}
