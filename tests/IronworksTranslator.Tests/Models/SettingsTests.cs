using IronworksTranslator.Models.Enums;
using IronworksTranslator.Models.Settings;
using IronworksTranslator.Models.Translator;
using IronworksTranslator.Utils;
using Wpf.Ui.Appearance;

namespace IronworksTranslator.Tests.Models;

public class SettingsTests
{
    [Fact]
    public void CreateDefault_InitializesExpectedSettings()
    {
        var settings = IronworksSettings.CreateDefault();

        Assert.NotNull(settings.UiSettings);
        Assert.NotNull(settings.ChatUiSettings);
        Assert.NotNull(settings.TranslatorSettings);
        Assert.NotNull(settings.ChannelSettings);
        Assert.False(settings.UiSettings!.IsTosDisplayed);
        Assert.Equal(ApplicationTheme.Light, settings.UiSettings.Theme);
        Assert.Equal(400, settings.UiSettings.ChatWindowWidth);
        Assert.Equal(200, settings.UiSettings.ChatWindowHeight);
        Assert.Equal(TranslatorEngine.Papago, settings.TranslatorSettings!.TranslatorEngine);
        Assert.Equal(MiLMMTModelSize.MiLLMT_1B, settings.TranslatorSettings.MiLMMTModelSize);
        Assert.Equal(MiLMMTQuantization.Q8_0, settings.TranslatorSettings.MiLMMTQuantization);
    }

    [Fact]
    public void ChannelSettings_ContainsExpectedDefaultChannels()
    {
        var channels = new ChannelSettings();

        Assert.Equal(37, channels.ChatChannels.Count);
        Assert.Equal(channels.ChatChannels.Count, channels.ChatChannels.Select(channel => channel.Code).Distinct().Count());
        Assert.Equal(ChatCode.Echo, channels.Echo.Code);
        Assert.Equal("White", channels.Echo.Color);
        Assert.True(channels.Echo.Show);
        Assert.Equal(ClientLanguage.Japanese, channels.Echo.MajorLanguage);
        Assert.Equal(ChatCode.NPCDialog, channels.NpcDialog.Code);
        Assert.Equal("#ABD647", channels.NpcDialog.Color);
    }

    [Fact]
    public void MiLMMTModelProfiles_ResolveSupportedProfilesAndFallbackQuantization()
    {
        Assert.True(MiLMMTModelProfiles.IsSupported(MiLMMTModelSize.MiLLMT_1B, MiLMMTQuantization.Q8_0));
        Assert.False(MiLMMTModelProfiles.IsSupported(MiLMMTModelSize.MiLLMT_12B, MiLMMTQuantization.Q8_0));

        var fallback = MiLMMTModelProfiles.GetDefaultQuantization(MiLMMTModelSize.MiLLMT_12B);
        var profile = MiLMMTModelProfiles.Get(MiLMMTModelSize.MiLLMT_12B, fallback);

        Assert.Equal(MiLMMTQuantization.Q4_K_M, fallback);
        Assert.Contains("huggingface.co", profile.DownloadUrl);
        Assert.EndsWith(profile.FileName, profile.FilePath);
    }

    [Fact]
    public void NormalizeLegacySettingsYaml_ReplacesRemovedJaKoEngine()
    {
        var yaml = """
            translator_settings:
              translator_engine: Ironworks_Ja_Ko
            """;

        var normalized = IronworksSettings.NormalizeLegacySettingsYaml(yaml);

        Assert.Contains("translator_engine: MiLLMT", normalized);
        Assert.DoesNotContain("Ironworks_Ja_Ko", normalized);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void NormalizeSettings_MigratesLegacyJaKoAndMiLMMTNumericValues(int legacyValue)
    {
        var settings = IronworksSettings.CreateDefault();
        settings.TranslatorSettings!.TranslatorEngine = (TranslatorEngine)legacyValue;

        IronworksSettings.NormalizeSettings(settings);

        Assert.Equal(TranslatorEngine.MiLLMT, settings.TranslatorSettings.TranslatorEngine);
    }

    [Theory]
    [InlineData("NVIDIA GeForce RTX 4070", LocalModelDevicePriority.Cuda)]
    [InlineData("AMD Radeon RX 7900 XTX", LocalModelDevicePriority.Vulkan)]
    [InlineData("Intel Arc A770 Graphics", LocalModelDevicePriority.Vulkan)]
    public void LocalModelDevicePrioritySelector_RecommendsBackendFromAdapterName(
        string adapterName,
        LocalModelDevicePriority expectedPriority)
    {
        var priority = LocalModelDevicePrioritySelector.GetRecommendedPriority(adapterName);

        Assert.Equal(expectedPriority, priority);
    }

    [Fact]
    public void NormalizeSettings_UsesRecommendedDevicePriorityBeforeUserSelection()
    {
        var settings = IronworksSettings.CreateDefault();
        settings.TranslatorSettings!.LocalModelDevicePriority = LocalModelDevicePriority.Cuda;
        settings.TranslatorSettings.LocalModelDevicePriorityUserSelected = false;

        IronworksSettings.NormalizeSettings(settings, LocalModelDevicePriority.Vulkan);

        Assert.Equal(LocalModelDevicePriority.Vulkan, settings.TranslatorSettings.LocalModelDevicePriority);
    }

    [Fact]
    public void NormalizeSettings_PreservesUserSelectedDevicePriority()
    {
        var settings = IronworksSettings.CreateDefault();
        settings.TranslatorSettings!.LocalModelDevicePriority = LocalModelDevicePriority.Cuda;
        settings.TranslatorSettings.LocalModelDevicePriorityUserSelected = true;

        IronworksSettings.NormalizeSettings(settings, LocalModelDevicePriority.Vulkan);

        Assert.Equal(LocalModelDevicePriority.Cuda, settings.TranslatorSettings.LocalModelDevicePriority);
    }

    [Fact]
    public void TranslatorEngine_DoesNotExposeRemovedJaKoEngine()
    {
        var engines = Enum.GetValues<TranslatorEngine>();
        var expectedEngines = new[]
        {
            TranslatorEngine.Papago,
            TranslatorEngine.DeepL_API,
            TranslatorEngine.MiLLMT,
        };

        Assert.Equal(expectedEngines, engines);
        Assert.DoesNotContain("Ironworks_Ja_Ko", Enum.GetNames<TranslatorEngine>());
        Assert.Equal(2, (int)TranslatorEngine.MiLLMT);
    }
}
