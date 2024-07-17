using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using IronworksTranslator.Models.Enums;
using IronworksTranslator.Models.Settings;
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

        private void OnClientLanguageMessage(object recipient, PropertyChangedMessage<ClientLanguage> message)
        {
            if (recipient is not ChatChannel recipientChannel)
            {
                string errorMessage = $"ChatChannel: Unknown recipient {recipient}";
                Log.Error(errorMessage);
                MessageBox.Show(errorMessage);
                return;
            }

            var channelSettings = IronworksSettings.Instance.ChannelSettings;
            switch (message.PropertyName)
            {
                //case nameof(SettingsViewModel.GroupPartyFieldLanguage):
                //    if (recipientChannel.Code != ChatCode.GroupPartyField) break;
                //    MajorLanguage = message.NewValue;
                //    break;
                case nameof(SettingsViewModel.SayLanguage):
                    if (recipientChannel.Code != ChatCode.Say) break;
                    MajorLanguage = message.NewValue;
                    break;
                case nameof(SettingsViewModel.YellLanguage):
                    if (recipientChannel.Code != ChatCode.Yell) break;
                    MajorLanguage = message.NewValue;
                    break;
                case nameof(SettingsViewModel.ShoutLanguage):
                    if (recipientChannel.Code != ChatCode.Shout) break;
                    MajorLanguage = message.NewValue;
                    break;
                case nameof(SettingsViewModel.TellLanguage):
                    if (recipientChannel.Code != ChatCode.Tell) break;
                    MajorLanguage = message.NewValue;
                    break;
                case nameof(SettingsViewModel.PartyLanguage):
                    if (recipientChannel.Code != ChatCode.Party) break;
                    MajorLanguage = message.NewValue;
                    break;
                case nameof(SettingsViewModel.AllianceLanguage):
                    if (recipientChannel.Code != ChatCode.Alliance) break;
                    MajorLanguage = message.NewValue;
                    break;
                case nameof(SettingsViewModel.EmoteLanguage):
                    if (recipientChannel.Code != ChatCode.Emote) break;
                    MajorLanguage = message.NewValue;
                    break;
                case nameof(SettingsViewModel.EmoteCustomLanguage):
                    if (recipientChannel.Code != ChatCode.EmoteCustom) break;
                    MajorLanguage = message.NewValue;
                    break;
                case nameof(SettingsViewModel.FreecompanyLanguage):
                    if (recipientChannel.Code != ChatCode.FreeCompany) break;
                    MajorLanguage = message.NewValue;
                    break;
                case nameof(SettingsViewModel.NoviceLanguage):
                    if (recipientChannel.Code != ChatCode.Novice) break;
                    MajorLanguage = message.NewValue;
                    break;
                case nameof(SettingsViewModel.Linkshell1Language):
                    if (recipientChannel.Code != ChatCode.LinkShell1) break;
                    MajorLanguage = message.NewValue;
                    break;
                case nameof(SettingsViewModel.Linkshell2Language):
                    if (recipientChannel.Code != ChatCode.LinkShell2) break;
                    MajorLanguage = message.NewValue;
                    break;
                case nameof(SettingsViewModel.Linkshell3Language):
                    if (recipientChannel.Code != ChatCode.LinkShell3) break;
                    MajorLanguage = message.NewValue;
                    break;
                case nameof(SettingsViewModel.Linkshell4Language):
                    if (recipientChannel.Code != ChatCode.LinkShell4) break;
                    MajorLanguage = message.NewValue;
                    break;
                case nameof(SettingsViewModel.Linkshell5Language):
                    if (recipientChannel.Code != ChatCode.LinkShell5) break;
                    MajorLanguage = message.NewValue;
                    break;
                case nameof(SettingsViewModel.Linkshell6Language):
                    if (recipientChannel.Code != ChatCode.LinkShell6) break;
                    MajorLanguage = message.NewValue;
                    break;
                case nameof(SettingsViewModel.Linkshell7Language):
                    if (recipientChannel.Code != ChatCode.LinkShell7) break;
                    MajorLanguage = message.NewValue;
                    break;
                case nameof(SettingsViewModel.Linkshell8Language):
                    if (recipientChannel.Code != ChatCode.LinkShell8) break;
                    MajorLanguage = message.NewValue;
                    break;
                case nameof(SettingsViewModel.CwLinkshell1Language):
                    if (recipientChannel.Code != ChatCode.CWLinkShell1) break;
                    MajorLanguage = message.NewValue;
                    break;
                case nameof(SettingsViewModel.CwLinkshell2Language):
                    if (recipientChannel.Code != ChatCode.CWLinkShell2) break;
                    MajorLanguage = message.NewValue;
                    break;
                case nameof(SettingsViewModel.CwLinkshell3Language):
                    if (recipientChannel.Code != ChatCode.CWLinkShell3) break;
                    MajorLanguage = message.NewValue;
                    break;
                case nameof(SettingsViewModel.CwLinkshell4Language):
                    if (recipientChannel.Code != ChatCode.CWLinkShell4) break;
                    MajorLanguage = message.NewValue;
                    break;
                case nameof(SettingsViewModel.CwLinkshell5Language):
                    if (recipientChannel.Code != ChatCode.CWLinkShell5) break;
                    MajorLanguage = message.NewValue;
                    break;
                case nameof(SettingsViewModel.CwLinkshell6Language):
                    if (recipientChannel.Code != ChatCode.CWLinkShell6) break;
                    MajorLanguage = message.NewValue;
                    break;
                case nameof(SettingsViewModel.CwLinkshell7Language):
                    if (recipientChannel.Code != ChatCode.CWLinkShell7) break;
                    MajorLanguage = message.NewValue;
                    break;
                case nameof(SettingsViewModel.CwLinkshell8Language):
                    if (recipientChannel.Code != ChatCode.CWLinkShell8) break;
                    MajorLanguage = message.NewValue;
                    break;
                case nameof(SettingsViewModel.GameSystemLanguage):
                    if (recipientChannel.Code != ChatCode.System) break;
                    MajorLanguage = message.NewValue;
                    break;
                case nameof(SettingsViewModel.GameNoticeLanguage):
                    if (recipientChannel.Code != ChatCode.Notice) break;
                    MajorLanguage = message.NewValue;
                    break;
                case nameof(SettingsViewModel.GameErrorLanguage):
                    if (recipientChannel.Code != ChatCode.Error) break;
                    MajorLanguage = message.NewValue;
                    break;
                case nameof(SettingsViewModel.GatherLanguage):
                    if (recipientChannel.Code != ChatCode.Gather) break;
                    MajorLanguage = message.NewValue;
                    break;
                case nameof(SettingsViewModel.GilReceiveLanguage):
                    if (recipientChannel.Code != ChatCode.GilReceive) break;
                    MajorLanguage = message.NewValue;
                    break;
                case nameof(SettingsViewModel.MarketSoldLanguage):
                    if (recipientChannel.Code != ChatCode.MarketSold) break;
                    MajorLanguage = message.NewValue;
                    break;
                case nameof(SettingsViewModel.RecruitmentLanguage):
                    if (recipientChannel.Code != ChatCode.Recruitment) break;
                    MajorLanguage = message.NewValue;
                    break;
                case nameof(SettingsViewModel.NpcDialogLanguage):
                    if (recipientChannel.Code != ChatCode.NPCDialog) break;
                    MajorLanguage = message.NewValue;
                    break;
                case nameof(SettingsViewModel.NpcAnnounceLanguage):
                    if (recipientChannel.Code != ChatCode.NPCAnnounce) break;
                    MajorLanguage = message.NewValue;
                    break;
                case nameof(SettingsViewModel.BossQuotesLanguage):
                    if (recipientChannel.Code != ChatCode.BossQuotes) break;
                    MajorLanguage = message.NewValue;
                    break;
                default:
                    break;
            }
        }
    }
}
