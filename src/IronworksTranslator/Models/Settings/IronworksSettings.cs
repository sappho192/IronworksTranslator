using IronworksTranslator.Helpers;
using IronworksTranslator.Models.Enums;
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

        public static IronworksSettings CreateDefault()
        {
            return new IronworksSettings
            {
                UiSettings = new UISettings
                {
                    Theme = ApplicationTheme.Light
                },
                ChatUiSettings = new ChatUISettings
                {
                    ChatboxFontSize = 12
                },
                TranslatorSettings = new TranslatorSettings
                {
                    ClientLanguage = ClientLanguage.Korean,
                    TranslatorEngine = TranslatorEngine.Papago,
                    DialogueTranslationMethod = DialogueTranslationMethod.MemorySearch
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
    }
}
