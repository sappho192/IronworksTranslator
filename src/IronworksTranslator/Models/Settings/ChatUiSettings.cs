using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using Serilog;
using YamlDotNet.Serialization;
using IronworksTranslator.Utils;
using System.Windows.Media;
using ObservableCollections;
using IronworksTranslator.ViewModels.Pages;

namespace IronworksTranslator.Models.Settings
{
    public partial class ChatUISettings : ObservableRecipient
    {
        public ChatUISettings()
        {
            Messenger.Register<PropertyChangedMessage<int>>(this, OnIntMessage);
            Messenger.Register<PropertyChangedMessage<string>>(this, OnStringMessage);

            InitFontList();
        }

        private static void InitFontList()
        {
            var cond = System.Windows.Markup.XmlLanguage.GetLanguage(System.Globalization.CultureInfo.CurrentCulture.Name);
            foreach (var font in Fonts.SystemFontFamilies)
            {
                if (font.FamilyNames.ContainsKey(cond))
                {
                    systemFontList.Add(font.FamilyNames[cond]);
                }
                else
                {
                    systemFontList.Add(font.ToString());
                }
            }
            systemFontList.Sort();
            Log.Information($"Loaded {systemFontList.Count} system fonts.");
            systemFontList.Insert(0, "KoPubWorld Dotum");
        }

        public static bool CheckSpecificFontExists(ChatUISettings settings, string font)
        {
            if (!systemFontList.Contains(font))
            {
                UseDefaultFont(settings);
                return false;
            }
            return true;
        }

        private static void UseDefaultFont(ChatUISettings chatUISettings)
        {
            MessageBox.Show(string.Format(Localizer.GetString("app.settings.font_not_exist"), chatUISettings.Font));
            chatUISettings.Font = "KoPubWorld Dotum";
        }

        public static readonly List<string> systemFontList = [];

        [ObservableProperty]
        [property: YamlMember(Alias = "chatbox_font_size")]
        private int _chatboxFontSize;

        [ObservableProperty]
        [property: YamlMember(Alias = "font")]
        private string _font;

        [SaveSettingsOnChange]
        partial void OnChatboxFontSizeChanged(int value)
        {
            Log.Information($"Chatbox font size changed to {value}");
        }

        [SaveSettingsOnChange]
        partial void OnFontChanged(string value)
        {
            Log.Information($"Chatbox font changed to {value}");
        }

        private void OnIntMessage(object recipient, PropertyChangedMessage<int> message)
        {
            switch (message.PropertyName)
            {
                case nameof(SettingsViewModel.ChatBoxFontSize):
                    ChatboxFontSize = message.NewValue;
                    break;
            }
        }

        private void OnStringMessage(object recipient, PropertyChangedMessage<string> message)
        {
            switch (message.PropertyName)
            {
                case nameof(SettingsViewModel.CurrentFont):
                    Font = message.NewValue;
                    break;
            }
        }
    }
}
