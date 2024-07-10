using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using Serilog;
using YamlDotNet.Serialization;

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

        partial void OnChatboxFontSizeChanged(int value)
        {
            Log.Information($"Chatbox font size changed to {value}");
            if (IronworksSettings.Instance != null)
            {
                IronworksSettings.UpdateSettingsFile(IronworksSettings.Instance);
            }
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
