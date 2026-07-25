using IronworksTranslator.Models.Enums;
using IronworksTranslator.Models.Settings;
using IronworksTranslator.Models.Translator;
using IronworksTranslator.Utils;
using Wpf.Ui.Appearance;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

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
        Assert.Equal(MiLMMTModelSize.MiLMMT_1B, settings.TranslatorSettings.MiLMMTModelSize);
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
        Assert.True(MiLMMTModelProfiles.IsSupported(MiLMMTModelSize.MiLMMT_1B, MiLMMTQuantization.Q8_0));
        Assert.False(MiLMMTModelProfiles.IsSupported(MiLMMTModelSize.MiLMMT_12B, MiLMMTQuantization.Q8_0));

        var fallback = MiLMMTModelProfiles.GetDefaultQuantization(MiLMMTModelSize.MiLMMT_12B);
        var profile = MiLMMTModelProfiles.Get(MiLMMTModelSize.MiLMMT_12B, fallback);

        Assert.Equal(MiLMMTQuantization.Q4_K_M, fallback);
        Assert.Contains("huggingface.co", profile.DownloadUrl);
        Assert.EndsWith(profile.FileName, profile.FilePath);
    }

    [Fact]
    public void MiLMMTModelProfiles_UseCompactKoEnJaQ4ProfileAndRetainLegacyCleanupMetadata()
    {
        var compact = MiLMMTModelProfiles.Get(
            MiLMMTModelSize.MiLMMT_1B,
            MiLMMTQuantization.Q4_K_M);

        Assert.Equal("MiLMMT 1B Compact", compact.DisplayName);
        Assert.Equal(
            "sappho192/MiLMMT-46-1B-v0.1-ko-en-ja-pruned-130k-imatrix-Q4_K_M-GGUF",
            compact.Repository);
        Assert.Equal(
            "milmmt-pruned-130k-bf16-imatrix-mix-late16-25-ffngateupq8-Q4_K_M.gguf",
            compact.FileName);
        Assert.Equal(803547328, compact.FileSize);
        Assert.Equal(
            "5f781fdc9a685212dba3244b7cf2df39625776066043408f769549195f018b0d",
            compact.Sha256);
        Assert.True(compact.Supports(TranslationLanguageCode.Korean));
        Assert.True(compact.Supports(TranslationLanguageCode.English));
        Assert.True(compact.Supports(TranslationLanguageCode.Japanese));
        Assert.False(compact.Supports(TranslationLanguageCode.German));
        Assert.False(compact.Supports(TranslationLanguageCode.French));

        var retired = Assert.Single(MiLMMTModelProfiles.Retired);
        Assert.DoesNotContain(retired, MiLMMTModelProfiles.All);
        Assert.Equal("MiLMMT-46-1B-v0.1.Q4_K_M.gguf", retired.FileName);
        Assert.NotEqual(retired, compact);
        Assert.Equal(
            new[] { retired },
            MiLMMTModelProfiles.GetDownloadedRetiredProfiles(profile => profile == retired));
    }

    [Fact]
    public void MiLMMTModelProfiles_PreferredAvailableProfile_UsesDownloadedFallbackForSelectedSize()
    {
        var expected = MiLMMTModelProfiles.Get(
            MiLMMTModelSize.MiLMMT_4B,
            MiLMMTQuantization.Q4_K_M);

        var selected = MiLMMTModelProfiles.FindPreferredAvailableProfile(
            MiLMMTModelSize.MiLMMT_4B,
            MiLMMTQuantization.Q8_0,
            profile => profile == expected);

        Assert.Equal(expected, selected);
    }

    [Fact]
    public void MiLMMTModelProfiles_PreferredAvailableProfile_PreservesAvailablePreferredQuantization()
    {
        var expected = MiLMMTModelProfiles.Get(
            MiLMMTModelSize.MiLMMT_4B,
            MiLMMTQuantization.Q8_0);

        var selected = MiLMMTModelProfiles.FindPreferredAvailableProfile(
            MiLMMTModelSize.MiLMMT_4B,
            MiLMMTQuantization.Q8_0,
            profile => profile.Quantization is MiLMMTQuantization.Q4_K_M or MiLMMTQuantization.Q8_0);

        Assert.Equal(expected, selected);
    }

    [Fact]
    public void NormalizeLegacySettingsYaml_ReplacesRemovedJaKoEngine()
    {
        var yaml = """
            translator_settings:
              translator_engine: Ironworks_Ja_Ko
            """;

        var normalized = IronworksSettings.NormalizeLegacySettingsYaml(yaml);

        Assert.Contains("translator_engine: MiLMMT", normalized);
        Assert.DoesNotContain("Ironworks_Ja_Ko", normalized);
    }

    [Fact]
    public void NormalizeLegacySettingsYaml_CorrectsMiLLMTEnumNames()
    {
        var yaml = """
            translator_settings:
              translator_engine: MiLLMT
              milmmt_model_size: MiLLMT_4B
            """;

        var normalized = IronworksSettings.NormalizeLegacySettingsYaml(yaml);

        Assert.Contains("translator_engine: MiLMMT", normalized);
        Assert.Contains("milmmt_model_size: MiLMMT_4B", normalized);
        Assert.DoesNotContain("MiLLMT", normalized);
    }

    [Fact]
    public void SettingsDeserializer_IgnoresUnknownProperties()
    {
        var yaml = """
            ui_settings:
              is_tos_displayed: false
            chat_ui_settings:
              font: KoPubWorld Dotum
            translator_settings:
              translator_engine: Papago
              unknown_future_property: true
            channel_settings:
              preset_name: Default
            """;
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        var settings = deserializer.Deserialize<IronworksSettings>(yaml);

        Assert.Equal(TranslatorEngine.Papago, settings.TranslatorSettings!.TranslatorEngine);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void NormalizeSettings_MigratesLegacyJaKoAndMiLMMTNumericValues(int legacyValue)
    {
        var settings = IronworksSettings.CreateDefault();
        settings.TranslatorSettings!.TranslatorEngine = (TranslatorEngine)legacyValue;

        IronworksSettings.NormalizeSettings(settings);

        Assert.Equal(TranslatorEngine.MiLMMT, settings.TranslatorSettings.TranslatorEngine);
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
            TranslatorEngine.MiLMMT,
        };

        Assert.Equal(expectedEngines, engines);
        Assert.DoesNotContain("Ironworks_Ja_Ko", Enum.GetNames<TranslatorEngine>());
        Assert.Equal(2, (int)TranslatorEngine.MiLMMT);
    }
}
