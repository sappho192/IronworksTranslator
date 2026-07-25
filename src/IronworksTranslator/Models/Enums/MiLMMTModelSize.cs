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
    }
}
