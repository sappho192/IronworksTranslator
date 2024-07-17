using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using IronworksTranslator.Models.Enums;
using IronworksTranslator.Utils;
using IronworksTranslator.ViewModels.Pages;
using Serilog;
using YamlDotNet.Serialization;

namespace IronworksTranslator.Models
{

    public partial class ChatChannel : ObservableRecipient
    {
        public ChatChannel()
        {
            Messenger.Register<PropertyChangedMessage<bool>>(this, OnBoolMessage);
            Messenger.Register<PropertyChangedMessage<ClientLanguage>>(this, OnClientLanguageMessage);
        }

        [ObservableProperty]
        [property: YamlMember(Alias = "show")]
        private bool _show;

        [ObservableProperty]
        [property: YamlMember(Alias = "code")]
        private ChatCode _code;

        [ObservableProperty]
        [property: YamlMember(Alias = "major_language")]
        private ClientLanguage _majorLanguage;

        [SaveSettingsOnChange]
        partial void OnShowChanged(bool value)
        {
            Log.Information($"{Code}: Show changed to {value}");
        }

        [SaveSettingsOnChange]
        partial void OnMajorLanguageChanged(ClientLanguage value)
        {
            Log.Information($"{Code}: MajorLanguage changed to {value}");
        }

        private void OnBoolMessage(object recipient, PropertyChangedMessage<bool> message)
        {
            //switch (message.PropertyName)
            //{
            //    case nameof(SettingsViewModel.ChatChannelShow):
            Show = message.NewValue;
            //        break;
            //}
        }

        private const string groupSystem = nameof(ChatCode.GroupSystem);
        private void OnClientLanguageMessage(object recipient, PropertyChangedMessage<ClientLanguage> message)
        {
            if (recipient is not ChatChannel recipientChannel)
            {
                string errorMessage = $"ChatChannel: Unknown recipient {recipient}";
                Log.Error(errorMessage);
                MessageBox.Show(errorMessage);
                return;
            }
            
            switch (message.PropertyName)
            {
                case nameof(SettingsViewModel.SayLanguage):
                    if (recipientChannel.Code != ChatCode.Say) break;
                    goto case groupSystem;
                case nameof(SettingsViewModel.YellLanguage):
                    if (recipientChannel.Code != ChatCode.Yell) break;
                    goto case groupSystem;
                case nameof(SettingsViewModel.ShoutLanguage):
                    if (recipientChannel.Code != ChatCode.Shout) break;
                    goto case groupSystem;
                case nameof(SettingsViewModel.TellLanguage):
                    if (recipientChannel.Code != ChatCode.Tell) break;
                    goto case groupSystem;
                case nameof(SettingsViewModel.PartyLanguage):
                    if (recipientChannel.Code != ChatCode.Party) break;
                    goto case groupSystem;
                case nameof(SettingsViewModel.AllianceLanguage):
                    if (recipientChannel.Code != ChatCode.Alliance) break;
                    goto case groupSystem;
                case nameof(SettingsViewModel.EmoteLanguage):
                    if (recipientChannel.Code != ChatCode.Emote) break;
                    goto case groupSystem;
                case nameof(SettingsViewModel.EmoteCustomLanguage):
                    if (recipientChannel.Code != ChatCode.EmoteCustom) break;
                    goto case groupSystem;
                case groupSystem:
                    MajorLanguage = message.NewValue;
                    break;
                default:
                    string errorMessage = $"OnClientLanguageMessage: Unhandled channel: {recipientChannel.Code}";
                    Log.Error(errorMessage);
                    MessageBox.Show(errorMessage);
                    break;
            }
        }
    }
}
