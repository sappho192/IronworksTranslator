using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using IronworksTranslator.ViewModels.Pages;
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
    public void ComposeSettings_OldYamlDefaultsToCompactAndNewSettingsRoundTrip()
    {
        var deserializer = new DeserializerBuilder().Build();
        var settings = deserializer.Deserialize<TranslatorSettings>("client_language: Korean");
        Assert.False(settings.ComposeShowDetails);
        Assert.Equal(TranslationLanguageCode.Korean, settings.ComposeSourceLanguage);
        Assert.Equal(TranslationLanguageCode.Japanese, settings.ComposeTargetLanguage);
        settings.ComposeSourceLanguage = TranslationLanguageCode.English;
        settings.ComposeTargetLanguage = TranslationLanguageCode.French;
        settings.ComposeShowDetails = true;
        var restored = deserializer.Deserialize<TranslatorSettings>(new SerializerBuilder().Build().Serialize(settings));
        Assert.True(restored.ComposeShowDetails);
        Assert.Equal(TranslationLanguageCode.English, restored.ComposeSourceLanguage);
        Assert.Equal(TranslationLanguageCode.French, restored.ComposeTargetLanguage);
    }

    [Fact]
    public void CreateDefault_InitializesExpectedSettings()
    {
        var settings = IronworksSettings.CreateDefault();

        Assert.NotNull(settings.UiSettings);
        Assert.NotNull(settings.ChatUiSettings);
        Assert.NotNull(settings.TranslatorSettings);
        Assert.NotNull(settings.ChannelSettings);
        Assert.False(settings.UiSettings!.IsTosDisplayed);
        Assert.Equal(string.Empty, settings.UiSettings.LastReleaseNotesAppVersion);
        Assert.True(settings.UiSettings.FollowGameWindow);
        Assert.True(settings.UiSettings.HideTranslationWindowsFromAltTab);
        Assert.False(settings.ChatUiSettings.ShowDialogueInChat);
        Assert.Equal(ApplicationTheme.Light, settings.UiSettings.Theme);
        Assert.Equal(400, settings.UiSettings.ChatWindowWidth);
        Assert.Equal(200, settings.UiSettings.ChatWindowHeight);
        Assert.Equal(TranslatorEngine.Papago, settings.TranslatorSettings!.TranslatorEngine);
        Assert.Equal(MiLMMTModelSize.Ironworks_1B, settings.TranslatorSettings.MiLMMTModelSize);
        Assert.Equal(MiLMMTQuantization.Q8_0, settings.TranslatorSettings.MiLMMTQuantization);
    }

    [Fact]
    public void DialogueInChat_OldYamlDefaultsOffAndNewValueRoundTrips()
    {
        var deserializer = new DeserializerBuilder().Build();
        var oldSettings = deserializer.Deserialize<ChatUISettings>("chatbox_font_size: 14");
        Assert.False(oldSettings.ShowDialogueInChat);
        var enabled = deserializer.Deserialize<ChatUISettings>("show_dialogue_in_chat: true");
        Assert.True(enabled.ShowDialogueInChat);
        var yaml = new SerializerBuilder().Build().Serialize(enabled);
        Assert.Contains("show_dialogue_in_chat: true", yaml);
        Assert.True(deserializer.Deserialize<ChatUISettings>(yaml).ShowDialogueInChat);
    }

    [Fact]
    public void ReleaseNotes_OldYamlDefaultsEmptyAndVersionRecordRoundTrips()
    {
        var deserializer = new DeserializerBuilder().Build();
        var oldSettings = deserializer.Deserialize<UISettings>("is_tos_displayed: true");
        try
        {
            Assert.Equal(string.Empty, oldSettings.LastReleaseNotesAppVersion);
            oldSettings.LastReleaseNotesAppVersion = "2.2.0-beta.2";
            var yaml = new SerializerBuilder().Build().Serialize(oldSettings);
            Assert.Contains("last_release_notes_app_version: 2.2.0-beta.2", yaml);
            var restored = deserializer.Deserialize<UISettings>(yaml);
            try { Assert.Equal("2.2.0-beta.2", restored.LastReleaseNotesAppVersion); }
            finally { WeakReferenceMessenger.Default.UnregisterAll(restored); }
        }
        finally { WeakReferenceMessenger.Default.UnregisterAll(oldSettings); }
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void WindowBehavior_OldYamlDefaultsOnAndExplicitValuesRoundTrip(bool follow, bool hide)
    {
        var settings = IronworksSettings.DeserializeSettings("ui_settings:\n  theme: Light");
        Assert.True(settings.UiSettings!.FollowGameWindow);
        Assert.True(settings.UiSettings.HideTranslationWindowsFromAltTab);

        settings.UiSettings.FollowGameWindow = follow;
        settings.UiSettings.HideTranslationWindowsFromAltTab = hide;
        var yaml = IronworksSettings.SerializeSettings(settings);
        Assert.Contains($"follow_game_window: {follow.ToString().ToLowerInvariant()}", yaml);
        Assert.Contains($"hide_translation_windows_from_alt_tab: {hide.ToString().ToLowerInvariant()}", yaml);
        var restored = IronworksSettings.DeserializeSettings(yaml);
        Assert.Equal(follow, restored.UiSettings!.FollowGameWindow);
        Assert.Equal(hide, restored.UiSettings.HideTranslationWindowsFromAltTab);
    }

    [Fact]
    public void WindowBehavior_MessengerUpdatesPersistedProperties()
    {
        var settings = new UISettings();
        try
        {
            foreach (var enabled in new[] { false, true })
            {
                WeakReferenceMessenger.Default.Send(new PropertyChangedMessage<bool>(
                    this, nameof(SettingsViewModel.FollowGameWindow), !enabled, enabled));
                WeakReferenceMessenger.Default.Send(new PropertyChangedMessage<bool>(
                    this, nameof(SettingsViewModel.HideTranslationWindowsFromAltTab), !enabled, enabled));
                Assert.Equal(enabled, settings.FollowGameWindow);
                Assert.Equal(enabled, settings.HideTranslationWindowsFromAltTab);
            }
        }
        finally
        {
            WeakReferenceMessenger.Default.UnregisterAll(settings);
        }
    }

    [Fact]
    public void DialogueInChat_SettingsMessageUpdatesPersistedProperty()
    {
        var settings = new ChatUISettings();
        try
        {
            WeakReferenceMessenger.Default.Send(new PropertyChangedMessage<bool>(
                this, nameof(SettingsViewModel.ShowDialogueInChat), false, true));
            Assert.True(settings.ShowDialogueInChat);
            Assert.Contains("show_dialogue_in_chat: true", new SerializerBuilder().Build().Serialize(settings));
            WeakReferenceMessenger.Default.Send(new PropertyChangedMessage<bool>(
                this, nameof(SettingsViewModel.ShowDialogueInChat), true, false));
            Assert.False(settings.ShowDialogueInChat);
        }
        finally
        {
            WeakReferenceMessenger.Default.UnregisterAll(settings);
        }
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
    public void MiLMMTModelProfiles_ResolveSupportedProfilesAndFallbackMapping()
    {
        Assert.Equal(
            [MiLMMTModelSize.Ironworks_1B, MiLMMTModelSize.Ironworks_4B],
            MiLMMTModelProfiles.SelectableModelSizes);
        Assert.Equal(0, MiLMMTModelProfiles.GetSelectableModelSizeIndex(MiLMMTModelSize.Ironworks_1B));
        Assert.Equal(1, MiLMMTModelProfiles.GetSelectableModelSizeIndex(MiLMMTModelSize.Ironworks_4B));

        Assert.True(MiLMMTModelProfiles.IsSupported(MiLMMTModelSize.Ironworks_1B, MiLMMTQuantization.Q8_0));
        Assert.True(MiLMMTModelProfiles.IsSupported(MiLMMTModelSize.Ironworks_1B, MiLMMTQuantization.Q4_K_M));
        Assert.True(MiLMMTModelProfiles.IsSupported(MiLMMTModelSize.Ironworks_4B, MiLMMTQuantization.Q4_K_M));
        Assert.True(MiLMMTModelProfiles.IsSupported(MiLMMTModelSize.Ironworks_4B, MiLMMTQuantization.Q8_0));
        Assert.False(MiLMMTModelProfiles.IsSupported(MiLMMTModelSize.MiLMMT_1B, MiLMMTQuantization.Q8_0));
        Assert.False(MiLMMTModelProfiles.IsSupported(MiLMMTModelSize.MiLMMT_4B, MiLMMTQuantization.Q4_K_M));
        Assert.False(MiLMMTModelProfiles.IsSupported(MiLMMTModelSize.MiLMMT_12B, MiLMMTQuantization.Q4_K_M));

        Assert.Equal(MiLMMTModelSize.Ironworks_1B, MiLMMTModelProfiles.GetFallbackModelSize(MiLMMTModelSize.MiLMMT_1B));
        Assert.Equal(MiLMMTModelSize.Ironworks_1B, MiLMMTModelProfiles.GetFallbackModelSize(MiLMMTModelSize.MiLMMT_1B_Compact));
        Assert.Equal(MiLMMTModelSize.Ironworks_4B, MiLMMTModelProfiles.GetFallbackModelSize(MiLMMTModelSize.MiLMMT_4B));
        Assert.Equal(MiLMMTModelSize.Ironworks_4B, MiLMMTModelProfiles.GetFallbackModelSize(MiLMMTModelSize.MiLMMT_12B));

        Assert.Equal(MiLMMTQuantization.Q8_0, MiLMMTModelProfiles.GetDefaultQuantization(MiLMMTModelSize.Ironworks_1B));
        Assert.Equal(MiLMMTQuantization.Q4_K_M, MiLMMTModelProfiles.GetDefaultQuantization(MiLMMTModelSize.Ironworks_4B));

        var profile = MiLMMTModelProfiles.Get(MiLMMTModelSize.Ironworks_4B, MiLMMTQuantization.Q4_K_M);
        Assert.Contains("huggingface.co", profile.DownloadUrl);
        Assert.EndsWith(profile.FileName, profile.FilePath);
    }

    [Fact]
    public void MiLMMTModelProfiles_ExposeOfficialV02ModelsAndRetireStockMiLMMT()
    {
        var iw1bQ8 = MiLMMTModelProfiles.Get(MiLMMTModelSize.Ironworks_1B, MiLMMTQuantization.Q8_0);
        Assert.Equal("Ironworks Translator 1B v0.2 Q8_0", iw1bQ8.DisplayName);
        Assert.Equal("sappho192/ironworks-translator-1b-v0.2", iw1bQ8.Repository);
        Assert.Equal("ironworks-translator-1b-v0.2.Q8_0.gguf", iw1bQ8.FileName);
        Assert.Equal(1069305088, iw1bQ8.FileSize);
        Assert.Equal("981e606ddfd368ec81bd6a74717587e69feb577be70f45298d43f703555b6a91", iw1bQ8.Sha256);
        Assert.EndsWith("ironworks-translator-1b-v0.2", iw1bQ8.DirectoryPath);
        Assert.True(iw1bQ8.SupportsGlossaryPrompt);
        Assert.True(iw1bQ8.SupportsStylePrompt);
        Assert.True(iw1bQ8.Supports(TranslationLanguageCode.German));
        Assert.True(iw1bQ8.Supports(TranslationLanguageCode.French));

        var iw1bQ4 = MiLMMTModelProfiles.Get(MiLMMTModelSize.Ironworks_1B, MiLMMTQuantization.Q4_K_M);
        Assert.Equal("Ironworks Translator 1B v0.2 Q4_K_M", iw1bQ4.DisplayName);
        Assert.Equal(806056960, iw1bQ4.FileSize);
        Assert.Equal("4c2fd57f958feb39c3576dc109f3860925c1abf2aed5b7bb203ded25779722ae", iw1bQ4.Sha256);
        Assert.True(iw1bQ4.SupportsGlossaryPrompt);
        Assert.True(iw1bQ4.SupportsStylePrompt);

        var iw4bQ4 = MiLMMTModelProfiles.Get(MiLMMTModelSize.Ironworks_4B, MiLMMTQuantization.Q4_K_M);
        Assert.Equal("Ironworks Translator 4B v0.2 Q4_K_M", iw4bQ4.DisplayName);
        Assert.Equal("sappho192/ironworks-translator-4b-v0.2", iw4bQ4.Repository);
        Assert.Equal("ironworks-translator-4b-v0.2.Q4_K_M.gguf", iw4bQ4.FileName);
        Assert.True(iw4bQ4.SupportsGlossaryPrompt);
        Assert.True(iw4bQ4.SupportsStylePrompt);

        var iw4bQ8 = MiLMMTModelProfiles.Get(MiLMMTModelSize.Ironworks_4B, MiLMMTQuantization.Q8_0);
        Assert.Equal("Ironworks Translator 4B v0.2 Q8_0", iw4bQ8.DisplayName);
        Assert.Equal("ironworks-translator-4b-v0.2.Q8_0.gguf", iw4bQ8.FileName);
        Assert.True(iw4bQ8.SupportsGlossaryPrompt);
        Assert.True(iw4bQ8.SupportsStylePrompt);

        var retiredFallbacks = MiLMMTModelProfiles.Retired
            .Where(profile => profile.AllowTemporaryFallback)
            .ToArray();
        Assert.Equal(14, retiredFallbacks.Length);
        Assert.Contains(retiredFallbacks, p => p.FileName == "MiLMMT-46-1B-v1.0.Q8_0.gguf");
        Assert.Contains(retiredFallbacks, p => p.FileName == "MiLMMT-46-4B-v1.0.Q4_K_M.gguf");
        Assert.Contains(retiredFallbacks, p => p.FileName == "MiLMMT-46-12B-v1.0.Q4_K_M.gguf");
        Assert.Contains(retiredFallbacks, p => p.FileName == "ironworks-translator-1b-v0.1.Q8_0.gguf" && p.SupportsGlossaryPrompt && !p.SupportsStylePrompt);
        Assert.Contains(retiredFallbacks, p => p.FileName == "ironworks-translator-4b-v0.1.Q4_K_M.gguf" && p.SupportsGlossaryPrompt && !p.SupportsStylePrompt);

        var retiredCompact = Assert.Single(
            MiLMMTModelProfiles.Retired,
            profile => profile.Size == MiLMMTModelSize.MiLMMT_1B_Compact);
        Assert.False(retiredCompact.AllowTemporaryFallback);
        Assert.DoesNotContain(MiLMMTModelSize.MiLMMT_1B_Compact, MiLMMTModelProfiles.SelectableModelSizes);
        Assert.Equal(
            new[] { retiredCompact },
            MiLMMTModelProfiles.GetDownloadedRetiredProfiles(profile => profile == retiredCompact));
    }

    [Fact]
    public void MiLMMTModelProfiles_ResolveEffectiveProfile_PrefersV02ThenMatchingNewestFallback()
    {
        var target4BQ4 = MiLMMTModelProfiles.Get(MiLMMTModelSize.Ironworks_4B, MiLMMTQuantization.Q4_K_M);
        var retiredIw4Bv01Q4 = MiLMMTModelProfiles.Retired.Single(p =>
            p.FileName == "ironworks-translator-4b-v0.1.Q4_K_M.gguf");
        var retiredStock4Bv10Q4 = MiLMMTModelProfiles.Retired.Single(p =>
            p.FileName == "MiLMMT-46-4B-v1.0.Q4_K_M.gguf");
        var retiredStock4Bv01Q8 = MiLMMTModelProfiles.Retired.Single(p =>
            p.FileName == "MiLMMT-46-4B-v0.1.Q8_0.gguf");
        var retiredStock1Bv10Q8 = MiLMMTModelProfiles.Retired.Single(p =>
            p.FileName == "MiLMMT-46-1B-v1.0.Q8_0.gguf");

        Assert.Equal(
            target4BQ4,
            MiLMMTModelProfiles.ResolveEffectiveProfile(target4BQ4, p => p == target4BQ4 || p == retiredIw4Bv01Q4));
        Assert.Equal(
            retiredIw4Bv01Q4,
            MiLMMTModelProfiles.ResolveEffectiveProfile(target4BQ4, p => p == retiredStock1Bv10Q8 || p == retiredStock4Bv10Q4 || p == retiredIw4Bv01Q4));
        Assert.Equal(
            retiredStock4Bv10Q4,
            MiLMMTModelProfiles.ResolveEffectiveProfile(target4BQ4, p => p == retiredStock1Bv10Q8 || p == retiredStock4Bv10Q4 || p == retiredStock4Bv01Q8));
        Assert.Equal(
            retiredStock4Bv01Q8,
            MiLMMTModelProfiles.ResolveEffectiveProfile(target4BQ4, p => p == retiredStock1Bv10Q8 || p == retiredStock4Bv01Q8));
        Assert.Equal(
            retiredStock1Bv10Q8,
            MiLMMTModelProfiles.ResolveEffectiveProfile(target4BQ4, p => p == retiredStock1Bv10Q8));
        Assert.Null(
            MiLMMTModelProfiles.ResolveEffectiveProfile(target4BQ4, _ => false));
    }

    [Fact]
    public void MiLMMTModelProfiles_PreferredAvailableProfile_UsesDownloadedFallbackForSelectedSize()
    {
        var expected = MiLMMTModelProfiles.Get(
            MiLMMTModelSize.Ironworks_4B,
            MiLMMTQuantization.Q4_K_M);

        var selected = MiLMMTModelProfiles.FindPreferredAvailableProfile(
            MiLMMTModelSize.Ironworks_4B,
            MiLMMTQuantization.Q8_0,
            profile => profile == expected);

        Assert.Equal(expected, selected);
    }

    [Fact]
    public void MiLMMTModelProfiles_PreferredAvailableProfile_PreservesAvailablePreferredQuantization()
    {
        var expected = MiLMMTModelProfiles.Get(
            MiLMMTModelSize.Ironworks_4B,
            MiLMMTQuantization.Q8_0);

        var selected = MiLMMTModelProfiles.FindPreferredAvailableProfile(
            MiLMMTModelSize.Ironworks_4B,
            MiLMMTQuantization.Q8_0,
            profile => profile.Quantization is MiLMMTQuantization.Q4_K_M or MiLMMTQuantization.Q8_0);

        Assert.Equal(expected, selected);
    }

    [Fact]
    public void NormalizeSettings_MigratesRetiredStockMiLMMTSizePreservingSupportedQuantization()
    {
        var settings4b = IronworksSettings.CreateDefault();
        settings4b.TranslatorSettings!.MiLMMTModelSize = MiLMMTModelSize.MiLMMT_4B;
        settings4b.TranslatorSettings.MiLMMTQuantization = MiLMMTQuantization.Q8_0;
        IronworksSettings.NormalizeSettings(settings4b);
        Assert.Equal(MiLMMTModelSize.Ironworks_4B, settings4b.TranslatorSettings.MiLMMTModelSize);
        Assert.Equal(MiLMMTQuantization.Q8_0, settings4b.TranslatorSettings.MiLMMTQuantization);

        var settings12b = IronworksSettings.CreateDefault();
        settings12b.TranslatorSettings!.MiLMMTModelSize = MiLMMTModelSize.MiLMMT_12B;
        settings12b.TranslatorSettings.MiLMMTQuantization = MiLMMTQuantization.Q4_K_M;
        IronworksSettings.NormalizeSettings(settings12b);
        Assert.Equal(MiLMMTModelSize.Ironworks_4B, settings12b.TranslatorSettings.MiLMMTModelSize);
        Assert.Equal(MiLMMTQuantization.Q4_K_M, settings12b.TranslatorSettings.MiLMMTQuantization);

        var settings1b = IronworksSettings.CreateDefault();
        settings1b.TranslatorSettings!.MiLMMTModelSize = MiLMMTModelSize.MiLMMT_1B;
        settings1b.TranslatorSettings.MiLMMTQuantization = MiLMMTQuantization.Q4_K_M;
        IronworksSettings.NormalizeSettings(settings1b);
        Assert.Equal(MiLMMTModelSize.Ironworks_1B, settings1b.TranslatorSettings.MiLMMTModelSize);
        Assert.Equal(MiLMMTQuantization.Q4_K_M, settings1b.TranslatorSettings.MiLMMTQuantization);
    }

    [Fact]
    public void MiLMMTModelStorageItem_CompatibilityChangesOnlyAtMemoryThresholds()
    {
        var profile = MiLMMTModelProfiles.Get(
            MiLMMTModelSize.Ironworks_4B,
            MiLMMTQuantization.Q4_K_M);
        const ulong gib = 1024UL * 1024UL * 1024UL;

        var comfortable = new SystemResourceSnapshot(16 * gib, 8 * gib, null, null, null);
        var stillComfortable = new SystemResourceSnapshot(16 * gib, 8 * gib + 512UL * 1024UL * 1024UL, null, null, null);
        var tight = new SystemResourceSnapshot(16 * gib, 9 * gib, null, null, null);
        var insufficient = new SystemResourceSnapshot(16 * gib, 13 * gib, null, null, null);
        var unknown = new SystemResourceSnapshot(16 * gib, 8 * gib, null, null, null);

        Assert.Equal(
            MiLMMTResourceCompatibility.Comfortable,
            MiLMMTModelStorageItem.GetCompatibility(profile, comfortable, LocalModelDevicePriority.Cpu));
        Assert.Equal(
            MiLMMTResourceCompatibility.Tight,
            MiLMMTModelStorageItem.GetCompatibility(profile, tight, LocalModelDevicePriority.Cpu));
        Assert.Equal(
            MiLMMTResourceCompatibility.Insufficient,
            MiLMMTModelStorageItem.GetCompatibility(profile, insufficient, LocalModelDevicePriority.Cpu));
        Assert.Equal(
            MiLMMTResourceCompatibility.Unknown,
            MiLMMTModelStorageItem.GetCompatibility(profile, unknown, LocalModelDevicePriority.Cuda));
        Assert.False(MiLMMTModelStorageItem.HasCompatibilityChanged(
            [profile], comfortable, stillComfortable, LocalModelDevicePriority.Cpu));
        Assert.True(MiLMMTModelStorageItem.HasCompatibilityChanged(
            [profile], comfortable, tight, LocalModelDevicePriority.Cpu));
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

    [Theory]
    [InlineData("MiLLMT", "MiLLMT_4B")]
    [InlineData("MiLMMT", "MiLMMT_4B")]
    public void DeserializeSettings_AcceptsLegacyAndCorrectedMiLMMTWireValues(
        string engineValue,
        string modelSizeValue)
    {
        var yaml = $$"""
            ui_settings:
              is_tos_displayed: true
            chat_ui_settings:
              font: KoPubWorld Dotum
            translator_settings:
              translator_engine: {{engineValue}}
              milmmt_model_size: {{modelSizeValue}}
              milmmt_quantization: Q4_K_M
              local_model_device_priority: Cuda
              local_model_device_priority_user_selected: true
            channel_settings:
              preset_name: Default
            """;

        var settings = IronworksSettings.DeserializeSettings(yaml);

        Assert.Equal(TranslatorEngine.MiLMMT, settings.TranslatorSettings!.TranslatorEngine);
        Assert.Equal(MiLMMTModelSize.MiLMMT_4B, settings.TranslatorSettings.MiLMMTModelSize);
        Assert.Equal(MiLMMTQuantization.Q4_K_M, settings.TranslatorSettings.MiLMMTQuantization);
        Assert.Equal(LocalModelDevicePriority.Cuda, settings.TranslatorSettings.LocalModelDevicePriority);
        Assert.True(settings.TranslatorSettings.LocalModelDevicePriorityUserSelected);
    }

    [Fact]
    public void SerializeSettings_UsesCurrentSchemaMiLMMTWireValues()
    {
        var settings = IronworksSettings.CreateDefault();
        settings.TranslatorSettings!.TranslatorEngine = TranslatorEngine.MiLMMT;
        settings.TranslatorSettings.MiLMMTModelSize = MiLMMTModelSize.MiLMMT_4B;
        settings.TranslatorSettings.MiLMMTQuantization = MiLMMTQuantization.Q4_K_M;

        var serialized = IronworksSettings.SerializeSettings(settings);

        Assert.Contains("translator_engine: MiLMMT", serialized);
        Assert.Contains("milmmt_model_size: MiLMMT_4B", serialized);
        Assert.DoesNotContain("translator_engine: MiLLMT", serialized);
        Assert.DoesNotContain("milmmt_model_size: MiLLMT_4B", serialized);
    }

    [Fact]
    public void SerializeLegacyV1Settings_UsesBeta2MiLMMTWireValues()
    {
        var settings = IronworksSettings.CreateDefault();
        settings.TranslatorSettings!.TranslatorEngine = TranslatorEngine.MiLMMT;
        settings.TranslatorSettings.MiLMMTModelSize = MiLMMTModelSize.MiLMMT_4B;
        settings.TranslatorSettings.MiLMMTQuantization = MiLMMTQuantization.Q4_K_M;

        var serialized = IronworksSettings.SerializeLegacyV1Settings(settings);

        Assert.Contains("translator_engine: MiLLMT", serialized);
        Assert.Contains("milmmt_model_size: MiLLMT_4B", serialized);
        Assert.DoesNotContain("translator_engine: MiLMMT", serialized);
        Assert.DoesNotContain("milmmt_model_size: MiLMMT_4B", serialized);

        var beta2Deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
        var beta2Settings = beta2Deserializer.Deserialize<Beta2SettingsContract>(serialized);

        Assert.Equal(
            Beta2TranslatorEngine.MiLLMT,
            beta2Settings.TranslatorSettings!.TranslatorEngine);
        Assert.Equal(
            Beta2MiLMMTModelSize.MiLLMT_4B,
            beta2Settings.TranslatorSettings.MiLMMTModelSize);
    }

    [Fact]
    public void SerializeLegacyV1Settings_FallsBackFromNewerModelValues()
    {
        var settings = IronworksSettings.CreateDefault();
        settings.TranslatorSettings!.TranslatorEngine = TranslatorEngine.MiLMMT;
        settings.TranslatorSettings.MiLMMTModelSize = MiLMMTModelSize.MiLMMT_1B_Compact;

        var serialized = IronworksSettings.SerializeLegacyV1Settings(settings);
        var beta2Deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
        var beta2Settings = beta2Deserializer.Deserialize<Beta2SettingsContract>(serialized);

        Assert.Contains("milmmt_model_size: MiLLMT_1B", serialized);
        Assert.DoesNotContain("MiLMMT_1B_Compact", serialized);
        Assert.Equal(
            Beta2MiLMMTModelSize.MiLLMT_1B,
            beta2Settings.TranslatorSettings!.MiLMMTModelSize);
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
    [InlineData(true)]
    [InlineData(false)]
    public void DeserializeSettings_IgnoresRemovedUseInternalAddressAndDoesNotReserializeIt(bool legacyValue)
    {
        var yaml = $$"""
            ui_settings:
              is_tos_displayed: false
            chat_ui_settings:
              font: KoPubWorld Dotum
            translator_settings:
              translator_engine: Papago
              use_internal_address: {{legacyValue.ToString().ToLowerInvariant()}}
            channel_settings:
              preset_name: Default
            """;

        var settings = IronworksSettings.DeserializeSettings(yaml);
        var serialized = IronworksSettings.SerializeSettings(settings);

        Assert.Equal(TranslatorEngine.Papago, settings.TranslatorSettings!.TranslatorEngine);
        Assert.DoesNotContain("use_internal_address", serialized, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TranslatorSettings_DoesNotExposeRemovedUseInternalAddressProperty()
    {
        Assert.Null(typeof(TranslatorSettings).GetProperty("UseInternalAddress"));
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

    private sealed class Beta2SettingsContract
    {
        public Beta2TranslatorSettings? TranslatorSettings { get; set; }
    }

    private sealed class Beta2TranslatorSettings
    {
        public Beta2TranslatorEngine TranslatorEngine { get; set; }

        [YamlMember(Alias = "milmmt_model_size")]
        public Beta2MiLMMTModelSize MiLMMTModelSize { get; set; }
    }

    private enum Beta2TranslatorEngine
    {
        Papago = 0,
        DeepL_API = 1,
        MiLLMT = 2,
    }

    private enum Beta2MiLMMTModelSize
    {
        MiLLMT_1B = 0,
        MiLLMT_4B,
        MiLLMT_12B,
    }
}
