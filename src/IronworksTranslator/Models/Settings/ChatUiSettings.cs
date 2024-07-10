using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using Serilog;
using YamlDotNet.Serialization;
using IronworksTranslator.Utils;

namespace IronworksTranslator.Models.Settings
{
    public partial class ChatUISettings : ObservableRecipient
    {
        public ChatUISettings()
        {
            Messenger.Register<PropertyChangedMessage<int>>(this, OnIntMessage);
        }

        [ObservableProperty]
        [property: YamlMember(Alias = "chatbox_font_size")]
        private int _chatboxFontSize;

        [SaveSettingsOnChange]
        partial void OnChatboxFontSizeChanged(int value)
        {
            Log.Information($"Chatbox font size changed to {value}");
        }

        private void OnIntMessage(object s, PropertyChangedMessage<int> m)
        {
            switch (m.PropertyName)
            {
                case "ChatBoxFontSize":
                    ChatboxFontSize = m.NewValue;
                    break;
            }
        }
    }
}
