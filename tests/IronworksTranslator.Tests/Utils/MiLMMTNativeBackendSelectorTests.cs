using IronworksTranslator.Models.Enums;
using IronworksTranslator.Utils.Translator;
using System.IO;

namespace IronworksTranslator.Tests.Utils;

public class MiLMMTNativeBackendSelectorTests
{
    [Theory]
    [InlineData(LocalModelDevicePriority.Cuda, LocalModelDevicePriority.Cuda, LocalModelDevicePriority.Vulkan, LocalModelDevicePriority.Cpu)]
    [InlineData(LocalModelDevicePriority.Vulkan, LocalModelDevicePriority.Vulkan, LocalModelDevicePriority.Cuda, LocalModelDevicePriority.Cpu)]
    public void GetCandidates_PrioritizesTheConfiguredGpuThenCpu(
        LocalModelDevicePriority requested,
        LocalModelDevicePriority first,
        LocalModelDevicePriority second,
        LocalModelDevicePriority third)
    {
        var candidates = MiLMMTNativeBackendSelector.GetCandidates(requested, avx2Supported: true);

        Assert.Equal([first, second, third], candidates);
    }

    [Theory]
    [InlineData(LocalModelDevicePriority.Cuda)]
    [InlineData(LocalModelDevicePriority.Vulkan)]
    [InlineData(LocalModelDevicePriority.Cpu)]
    public void GetCandidates_UsesCpuOnlyWhenAvx2IsUnavailable(LocalModelDevicePriority requested)
    {
        var candidates = MiLMMTNativeBackendSelector.GetCandidates(requested, avx2Supported: false);

        Assert.Equal([LocalModelDevicePriority.Cpu], candidates);
    }

    [Fact]
    public void ResolveBackend_UsesTheSecondGpuPackWhenThePreferredPackFails()
    {
        var probed = new List<LocalModelDevicePriority>();

        var selected = MiLMMTNativeBackendSelector.ResolveBackend(
            LocalModelDevicePriority.Cuda,
            avx2Supported: true,
            candidate =>
            {
                probed.Add(candidate);
                return candidate == LocalModelDevicePriority.Vulkan;
            });

        Assert.Equal(LocalModelDevicePriority.Vulkan, selected);
        Assert.Equal(
            [LocalModelDevicePriority.Cuda, LocalModelDevicePriority.Vulkan],
            probed);
    }

    [Fact]
    public void ResolveBackend_UsesCpuWhenBothGpuPacksAreMissingOrUnavailable()
    {
        var selected = MiLMMTNativeBackendSelector.ResolveBackend(
            LocalModelDevicePriority.Vulkan,
            avx2Supported: true,
            _ => false);

        Assert.Equal(LocalModelDevicePriority.Cpu, selected);
    }

    [Fact]
    public void NativeBackendSession_KeepsTheFirstSelectionUntilTheProcessRestarts()
    {
        var session = new MiLMMTNativeBackendSession();
        var probeCalls = 0;

        var firstSelection = session.Select(
            LocalModelDevicePriority.Cuda,
            avx2Supported: true,
            candidate =>
            {
                probeCalls++;
                return candidate == LocalModelDevicePriority.Vulkan;
            });
        var secondSelection = session.Select(
            LocalModelDevicePriority.Cpu,
            avx2Supported: true,
            _ => throw new InvalidOperationException("The cached selection should be reused."));

        Assert.Equal(LocalModelDevicePriority.Vulkan, firstSelection);
        Assert.Equal(LocalModelDevicePriority.Vulkan, secondSelection);
        Assert.Equal(2, probeCalls);
    }

    [Fact]
    public void GetLlamaPath_UsesThePublishedRuntimePackLayout()
    {
        var path = MiLMMTNativeBackendSelector.GetLlamaPath(
            LocalModelDevicePriority.Cuda,
            "C:\\Ironworks");

        Assert.Equal(
            Path.Combine("C:\\Ironworks", "runtimes", "win-x64", "native", "milmmt-cuda", "llama.dll"),
            path);
    }
}
