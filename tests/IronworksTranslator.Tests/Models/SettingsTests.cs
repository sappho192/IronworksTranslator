using IronworksTranslator.Models.Enums;
using IronworksTranslator.Models.Settings;
using IronworksTranslator.Models.Translator;
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
}
