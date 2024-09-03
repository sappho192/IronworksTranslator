using IronworksTranslator.Helpers;
using IronworksTranslator.Models.Enums;
using IronworksTranslator.Utils;
using System.IO;
using Wpf.Ui.Appearance;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace IronworksTranslator.Models.Settings
{
    public class IronworksSettings
    {
        public static IronworksSettings? Instance { get; set; }

        public UISettings? UiSettings { get; set; }
        public ChatUISettings? ChatUiSettings { get; set; }
        public TranslatorSettings? TranslatorSettings { get; set; }
        public ChannelSettings? ChannelSettings { get; set; }

        [TraceMethod]
        public static IronworksSettings CreateDefault()
        {
            var currentLocale = System.Globalization.CultureInfo.CurrentCulture.Name;

            return new IronworksSettings
            {
                UiSettings = new UISettings
                {
                    Theme = ApplicationTheme.Light,
                    AppLanguage = currentLocale switch
                    {
                        "ko-KR" => AppLanguage.Korean,
                        _ => AppLanguage.English
                    },
                    DialogueWindowVisible = true,
                    ChatWindowWidth = 400,
                    ChatWindowHeight = 200,
                    DialogueWindowWidth = 400,
                    DialogueWindowHeight = 100,
                    ChatWindowTop = 100,
                    ChatWindowLeft = 50,
                    DialogueWindowTop = 50,
                    DialogueWindowLeft = 350,
                    ChatWindowScreen = @"\\.\DISPLAY1",
                    DialogueWindowScreen = @"\\.\DISPLAY1",
                },
                ChatUiSettings = new ChatUISettings
                {
                    ChatboxFontSize = 12,
                    Font = "KoPubWorld Dotum",
                    ChatMargin = 0,
                    IsDraggable = true,
                    IsResizable = true,
                    WindowOpacity = 1.0
                },
                TranslatorSettings = new TranslatorSettings
                {
                    ClientLanguage = ClientLanguage.Korean,
                    TranslatorEngine = TranslatorEngine.Papago,
                    DialogueTranslationMethod = DialogueTranslationMethod.MemorySearch,
                    DeeplApiKeys = [],
                    DeeplAutoSourceLanguage = false,
                    UseInternalAddress = false
                },
                ChannelSettings = new ChannelSettings
                {
                    PresetName = "Default",
                    Guid = Guid.NewGuid(),
                }
            };
        }

        public static void UpdateSettingsFile(IronworksSettings settings)
        {
            var serializer = new SerializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .WithTypeInspector(inspector => new SettingsTypeInspector(inspector))
                .Build();
            File.WriteAllText("settings.yaml", serializer.Serialize(settings));
        }

        public static bool IsSettingsFileInValid(IronworksSettings settings)
        {
            if (settings == null ||
                settings.UiSettings == null ||
                settings.ChatUiSettings == null ||
                settings.TranslatorSettings == null ||
                settings.ChannelSettings == null)
            {
                return true;
            }

            if (settings.ChatUiSettings.Font == null)
            {
                return true;
            }

            if (!ChatUISettings.CheckSpecificFontExists(settings.ChatUiSettings, settings.ChatUiSettings.Font))
            {
                return false;
            }

            return false;
        }
    }
}
