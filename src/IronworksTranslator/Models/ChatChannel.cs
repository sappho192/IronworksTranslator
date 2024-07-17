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

        private const string groupPartyField = nameof(ChatCode.GroupPartyField);
        private const string groupCommunity = nameof(ChatCode.GroupCommunity);
        private const string groupLinkShell = nameof(ChatCode.GroupLinkShell);
        private const string groupCWLinkShell = nameof(ChatCode.GroupCWLinkShell);
        private const string groupSystem= nameof(ChatCode.GroupSystem);
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
                    goto case groupPartyField;
                case nameof(SettingsViewModel.YellLanguage):
                    if (recipientChannel.Code != ChatCode.Yell) break;
                    goto case groupPartyField;
                case nameof(SettingsViewModel.ShoutLanguage):
                    if (recipientChannel.Code != ChatCode.Shout) break;
                    goto case groupPartyField;
                case nameof(SettingsViewModel.TellLanguage):
                    if (recipientChannel.Code != ChatCode.Tell) break;
                    goto case groupPartyField;
                case nameof(SettingsViewModel.PartyLanguage):
                    if (recipientChannel.Code != ChatCode.Party) break;
                    goto case groupPartyField;
                case nameof(SettingsViewModel.AllianceLanguage):
                    if (recipientChannel.Code != ChatCode.Alliance) break;
                    goto case groupPartyField;
                case nameof(SettingsViewModel.EmoteLanguage):
                    if (recipientChannel.Code != ChatCode.Emote) break;
                    goto case groupPartyField;
                case nameof(SettingsViewModel.EmoteCustomLanguage):
                    if (recipientChannel.Code != ChatCode.EmoteCustom) break;
                    goto case groupPartyField;
                case groupPartyField:
                    MajorLanguage = message.NewValue;
                    break;
                case nameof(SettingsViewModel.FreecompanyLanguage):
                    if (recipientChannel.Code != ChatCode.FreeCompany) break;
                    goto case groupCommunity;
                case nameof(SettingsViewModel.NoviceLanguage):
                    if (recipientChannel.Code != ChatCode.Novice) break;
                    goto case groupCommunity;
                case groupCommunity:
                    MajorLanguage = message.NewValue;
                    break;
                case nameof(SettingsViewModel.Linkshell1Language):
                    if (recipientChannel.Code != ChatCode.LinkShell1) break;
                    goto case groupLinkShell;
                case nameof(SettingsViewModel.Linkshell2Language):
                    if (recipientChannel.Code != ChatCode.LinkShell2) break;
                    goto case groupLinkShell;
                case nameof(SettingsViewModel.Linkshell3Language):
                    if (recipientChannel.Code != ChatCode.LinkShell3) break;
                    goto case groupLinkShell;
                case nameof(SettingsViewModel.Linkshell4Language):
                    if (recipientChannel.Code != ChatCode.LinkShell4) break;
                    goto case groupLinkShell;
                case nameof(SettingsViewModel.Linkshell5Language):
                    if (recipientChannel.Code != ChatCode.LinkShell5) break;
                    goto case groupLinkShell;
                case nameof(SettingsViewModel.Linkshell6Language):
                    if (recipientChannel.Code != ChatCode.LinkShell6) break;
                    goto case groupLinkShell;
                case nameof(SettingsViewModel.Linkshell7Language):
                    if (recipientChannel.Code != ChatCode.LinkShell7) break;
                    goto case groupLinkShell;
                case nameof(SettingsViewModel.Linkshell8Language):
                    if (recipientChannel.Code != ChatCode.LinkShell8) break;
                    goto case groupLinkShell;
                case groupLinkShell:
                    MajorLanguage = message.NewValue;
                    break;
                case nameof(SettingsViewModel.CwLinkshell1Language):
                    if (recipientChannel.Code != ChatCode.CWLinkShell1) break;
                    goto case groupCWLinkShell;
                case nameof(SettingsViewModel.CwLinkshell2Language):
                    if (recipientChannel.Code != ChatCode.CWLinkShell2) break;
                    goto case groupCWLinkShell;
                case nameof(SettingsViewModel.CwLinkshell3Language):
                    if (recipientChannel.Code != ChatCode.CWLinkShell3) break;
                    goto case groupCWLinkShell;
                case nameof(SettingsViewModel.CwLinkshell4Language):
                    if (recipientChannel.Code != ChatCode.CWLinkShell4) break;
                    goto case groupCWLinkShell;
                case nameof(SettingsViewModel.CwLinkshell5Language):
                    if (recipientChannel.Code != ChatCode.CWLinkShell5) break;
                    goto case groupCWLinkShell;
                case nameof(SettingsViewModel.CwLinkshell6Language):
                    if (recipientChannel.Code != ChatCode.CWLinkShell6) break;
                    goto case groupCWLinkShell;
                case nameof(SettingsViewModel.CwLinkshell7Language):
                    if (recipientChannel.Code != ChatCode.CWLinkShell7) break;
                    goto case groupCWLinkShell;
                case nameof(SettingsViewModel.CwLinkshell8Language):
                    if (recipientChannel.Code != ChatCode.CWLinkShell8) break;
                    goto case groupCWLinkShell;
                case groupCWLinkShell:
                    MajorLanguage = message.NewValue;
                    break;
                case nameof(SettingsViewModel.GameSystemLanguage):
                    if (recipientChannel.Code != ChatCode.System) break;
                    goto case groupSystem;
                case nameof(SettingsViewModel.GameNoticeLanguage):
                    if (recipientChannel.Code != ChatCode.Notice) break;
                    goto case groupSystem;
                case nameof(SettingsViewModel.GameErrorLanguage):
                    if (recipientChannel.Code != ChatCode.Error) break;
                    goto case groupSystem;
                case nameof(SettingsViewModel.GatherLanguage):
                    if (recipientChannel.Code != ChatCode.Gather) break;
                    goto case groupSystem;
                case nameof(SettingsViewModel.GilReceiveLanguage):
                    if (recipientChannel.Code != ChatCode.GilReceive) break;
                    goto case groupSystem;
                case nameof(SettingsViewModel.MarketSoldLanguage):
                    if (recipientChannel.Code != ChatCode.MarketSold) break;
                    goto case groupSystem;
                case nameof(SettingsViewModel.RecruitmentLanguage):
                    if (recipientChannel.Code != ChatCode.Recruitment) break;
                    goto case groupSystem;
                case nameof(SettingsViewModel.NpcDialogLanguage):
                    if (recipientChannel.Code != ChatCode.NPCDialog) break;
                    goto case groupSystem;
                case nameof(SettingsViewModel.NpcAnnounceLanguage):
                    if (recipientChannel.Code != ChatCode.NPCAnnounce) break;
                    goto case groupSystem;
                case nameof(SettingsViewModel.BossQuotesLanguage):
                    if (recipientChannel.Code != ChatCode.BossQuotes) break;
                    goto case groupSystem;
                case groupSystem:
                    MajorLanguage = message.NewValue;
                    break;
                default:
                    break;
            }
        }
    }
}
