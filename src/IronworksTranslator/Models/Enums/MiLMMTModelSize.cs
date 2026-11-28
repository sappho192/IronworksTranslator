using System.ComponentModel;

namespace IronworksTranslator.Models.Enums
{
    public enum MiLMMTModelSize
    {
        [Description("MiLMMT 1B")]
        MiLMMT_1B = 0,

        [Description("MiLMMT 4B")]
        MiLMMT_4B,

        [Description("MiLMMT 12B")]
        MiLMMT_12B,

        [Description("MiLMMT 1B Compact (Debug)")]
        MiLMMT_1B_Compact,

        [Description("Ironworks Translator 1B v0.2")]
        Ironworks_1B,

        [Description("Ironworks Translator 4B v0.2")]
        Ironworks_4B,
    }
}
