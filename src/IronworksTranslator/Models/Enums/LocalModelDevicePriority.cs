using System.ComponentModel;

namespace IronworksTranslator.Models.Enums
{
    public enum LocalModelDevicePriority
    {
        [Description("CUDA(NVIDIA) first")]
        Cuda = 0,

        [Description("Vulkan(AMD) first")]
        Vulkan,

        [Description("CPU only")]
        Cpu,
    }
}
