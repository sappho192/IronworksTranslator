using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using IronworksTranslator.Models.Enums;
using IronworksTranslator.Utils;
using IronworksTranslator.Utils.Aspect;
using IronworksTranslator.ViewModels.Pages;
using Serilog;
using System.Windows.Media;
using YamlDotNet.Serialization;

namespace IronworksTranslator.Models
{

    public partial class ChatChannel : ObservableRecipient
    {
        public ChatChannel()
        {
            Messenger.Register<PropertyChangedMessage<bool>>(this, OnBoolMessage);
            Messenger.Register<PropertyChangedMessage<ClientLanguage>>(this, OnClientLanguageMessage);
            Messenger.Register<PropertyChangedMessage<Color>>(this, OnColorMessage);
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

        [ObservableProperty]
        [property: YamlMember(Alias = "color")]
        private string? _color;

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

        [SaveSettingsOnChange]
        partial void OnColorChanged(string? value)
        {
            Log.Information($"{Code}: Color changed to {value}");
        }

        private void OnColorMessage(object recipient, PropertyChangedMessage<Color> message)
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
                case nameof(SettingsViewModel.EchoColor):
                    if (recipientChannel.Code != ChatCode.Echo) break;
                    Color = message.NewValue.ToString();
                    break;
                case nameof(SettingsViewModel.SayColor):
                    if (recipientChannel.Code != ChatCode.Say) break;
                    Color = message.NewValue.ToString();
                    break;
                case nameof(SettingsViewModel.YellColor):
                    if (recipientChannel.Code != ChatCode.Yell) break;
                    Color = message.NewValue.ToString();
                    break;
                case nameof(SettingsViewModel.ShoutColor):
                    if (recipientChannel.Code != ChatCode.Shout) break;
                    Color = message.NewValue.ToString();
                    break;
                case nameof(SettingsViewModel.TellColor):
                    if (recipientChannel.Code != ChatCode.Tell) break;
                    Color = message.NewValue.ToString();
                    break;
                case nameof(SettingsViewModel.PartyColor):
                    if (recipientChannel.Code != ChatCode.Party) break;
                    Color = message.NewValue.ToString();
                    break;
                case nameof(SettingsViewModel.AllianceColor):
                    if (recipientChannel.Code != ChatCode.Alliance) break;
                    Color = message.NewValue.ToString();
                    break;
                case nameof(SettingsViewModel.EmoteColor):
                    if (recipientChannel.Code != ChatCode.Emote) break;
                    Color = message.NewValue.ToString();
                    break;
                case nameof(SettingsViewModel.EmoteCustomColor):
                    if (recipientChannel.Code != ChatCode.EmoteCustom) break;
                    Color = message.NewValue.ToString();
                    break;
                case nameof(SettingsViewModel.FreecompanyColor):
                    if (recipientChannel.Code != ChatCode.FreeCompany) break;
                    Color = message.NewValue.ToString();
                    break;
                case nameof(SettingsViewModel.NoviceColor):
                    if (recipientChannel.Code != ChatCode.Novice) break;
                    Color = message.NewValue.ToString();
                    break;
                case nameof(SettingsViewModel.Linkshell1Color):
                    if (recipientChannel.Code != ChatCode.LinkShell1) break;
                    Color = message.NewValue.ToString();
                    break;
                case nameof(SettingsViewModel.Linkshell2Color):
                    if (recipientChannel.Code != ChatCode.LinkShell2) break;
                    Color = message.NewValue.ToString();
                    break;
                case nameof(SettingsViewModel.Linkshell3Color):
                    if (recipientChannel.Code != ChatCode.LinkShell3) break;
                    Color = message.NewValue.ToString();
                    break;
                case nameof(SettingsViewModel.Linkshell4Color):
                    if (recipientChannel.Code != ChatCode.LinkShell4) break;
                    Color = message.NewValue.ToString();
                    break;
                case nameof(SettingsViewModel.Linkshell5Color):
                    if (recipientChannel.Code != ChatCode.LinkShell5) break;
                    Color = message.NewValue.ToString();
                    break;
                case nameof(SettingsViewModel.Linkshell6Color):
                    if (recipientChannel.Code != ChatCode.LinkShell6) break;
                    Color = message.NewValue.ToString();
                    break;
                case nameof(SettingsViewModel.Linkshell7Color):
                    if (recipientChannel.Code != ChatCode.LinkShell7) break;
                    Color = message.NewValue.ToString();
                    break;
                case nameof(SettingsViewModel.Linkshell8Color):
                    if (recipientChannel.Code != ChatCode.LinkShell8) break;
                    Color = message.NewValue.ToString();
                    break;
                case nameof(SettingsViewModel.CwLinkshell1Color):
                    if (recipientChannel.Code != ChatCode.CWLinkShell1) break;
                    Color = message.NewValue.ToString();
                    break;
                case nameof(SettingsViewModel.CwLinkshell2Color):
                    if (recipientChannel.Code != ChatCode.CWLinkShell2) break;
                    Color = message.NewValue.ToString();
                    break;
                case nameof(SettingsViewModel.CwLinkshell3Color):
                    if (recipientChannel.Code != ChatCode.CWLinkShell3) break;
                    Color = message.NewValue.ToString();
                    break;
                case nameof(SettingsViewModel.CwLinkshell4Color):
                    if (recipientChannel.Code != ChatCode.CWLinkShell4) break;
                    Color = message.NewValue.ToString();
                    break;
                case nameof(SettingsViewModel.CwLinkshell5Color):
                    if (recipientChannel.Code != ChatCode.CWLinkShell5) break;
                    Color = message.NewValue.ToString();
                    break;
                case nameof(SettingsViewModel.CwLinkshell6Color):
                    if (recipientChannel.Code != ChatCode.CWLinkShell6) break;
                    Color = message.NewValue.ToString();
                    break;
                case nameof(SettingsViewModel.CwLinkshell7Color):
                    if (recipientChannel.Code != ChatCode.CWLinkShell7) break;
                    Color = message.NewValue.ToString();
                    break;
                case nameof(SettingsViewModel.CwLinkshell8Color):
                    if (recipientChannel.Code != ChatCode.CWLinkShell8) break;
                    Color = message.NewValue.ToString();
                    break;
                case nameof(SettingsViewModel.GameSystemColor):
                    if (recipientChannel.Code != ChatCode.System) break;
                    Color = message.NewValue.ToString();
                    break;
                case nameof(SettingsViewModel.GameNoticeColor):
                    if (recipientChannel.Code != ChatCode.Notice) break;
                    Color = message.NewValue.ToString();
                    break;
                case nameof(SettingsViewModel.GameErrorColor):
                    if (recipientChannel.Code != ChatCode.Error) break;
                    Color = message.NewValue.ToString();
                    break;
                case nameof(SettingsViewModel.GatherColor):
                    if (recipientChannel.Code != ChatCode.Gather) break;
                    Color = message.NewValue.ToString();
                    break;
                case nameof(SettingsViewModel.GilReceiveColor):
                    if (recipientChannel.Code != ChatCode.GilReceive) break;
                    Color = message.NewValue.ToString();
                    break;
                case nameof(SettingsViewModel.MarketSoldColor):
                    if (recipientChannel.Code != ChatCode.MarketSold) break;
                    Color = message.NewValue.ToString();
                    break;
                case nameof(SettingsViewModel.RecruitmentColor):
                    if (recipientChannel.Code != ChatCode.Recruitment) break;
                    Color = message.NewValue.ToString();
                    break;
                case nameof(SettingsViewModel.NpcDialogColor):
                    if (recipientChannel.Code != ChatCode.NPCDialog) break;
                    Color = message.NewValue.ToString();
                    break;
                case nameof(SettingsViewModel.NpcAnnounceColor):
                    if (recipientChannel.Code != ChatCode.NPCAnnounce) break;
                    Color = message.NewValue.ToString();
                    break;
                case nameof(SettingsViewModel.BossQuotesColor):
                    if (recipientChannel.Code != ChatCode.BossQuotes) break;
                    Color = message.NewValue.ToString();
                    break;
                default:
                    break;
            }
        }

        private void OnBoolMessage(object recipient, PropertyChangedMessage<bool> message)
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
                case nameof(SettingsViewModel.ShowEchoChannel):
                    if (recipientChannel.Code != ChatCode.Echo) break;
                    Show = message.NewValue;
                    break;
                case nameof(SettingsViewModel.ShowSayChannel):
                    if (recipientChannel.Code != ChatCode.Say) break;
                    Show = message.NewValue;
                    break;
                case nameof(SettingsViewModel.ShowYellChannel):
                    if (recipientChannel.Code != ChatCode.Yell) break;
                    Show = message.NewValue;
                    break;
                case nameof(SettingsViewModel.ShowShoutChannel):
                    if (recipientChannel.Code != ChatCode.Shout) break;
                    Show = message.NewValue;
                    break;
                case nameof(SettingsViewModel.ShowTellChannel):
                    if (recipientChannel.Code != ChatCode.Tell) break;
                    Show = message.NewValue;
                    break;
                case nameof(SettingsViewModel.ShowPartyChannel):
                    if (recipientChannel.Code != ChatCode.Party) break;
                    Show = message.NewValue;
                    break;
                case nameof(SettingsViewModel.ShowAllianceChannel):
                    if (recipientChannel.Code != ChatCode.Alliance) break;
                    Show = message.NewValue;
                    break;
                case nameof(SettingsViewModel.ShowEmoteChannel):
                    if (recipientChannel.Code != ChatCode.Emote) break;
                    Show = message.NewValue;
                    break;
                case nameof(SettingsViewModel.ShowEmoteCustomChannel):
                    if (recipientChannel.Code != ChatCode.EmoteCustom) break;
                    Show = message.NewValue;
                    break;
                case nameof(SettingsViewModel.ShowFreecompanyChannel):
                    if (recipientChannel.Code != ChatCode.FreeCompany) break;
                    Show = message.NewValue;
                    break;
                case nameof(SettingsViewModel.ShowNoviceChannel):
                    if (recipientChannel.Code != ChatCode.Novice) break;
                    Show = message.NewValue;
                    break;
                case nameof(SettingsViewModel.ShowLinkshell1Channel):
                    if (recipientChannel.Code != ChatCode.LinkShell1) break;
                    Show = message.NewValue;
                    break;
                case nameof(SettingsViewModel.ShowLinkshell2Channel):
                    if (recipientChannel.Code != ChatCode.LinkShell2) break;
                    Show = message.NewValue;
                    break;
                case nameof(SettingsViewModel.ShowLinkshell3Channel):
                    if (recipientChannel.Code != ChatCode.LinkShell3) break;
                    Show = message.NewValue;
                    break;
                case nameof(SettingsViewModel.ShowLinkshell4Channel):
                    if (recipientChannel.Code != ChatCode.LinkShell4) break;
                    Show = message.NewValue;
                    break;
                case nameof(SettingsViewModel.ShowLinkshell5Channel):
                    if (recipientChannel.Code != ChatCode.LinkShell5) break;
                    Show = message.NewValue;
                    break;
                case nameof(SettingsViewModel.ShowLinkshell6Channel):
                    if (recipientChannel.Code != ChatCode.LinkShell6) break;
                    Show = message.NewValue;
                    break;
                case nameof(SettingsViewModel.ShowLinkshell7Channel):
                    if (recipientChannel.Code != ChatCode.LinkShell7) break;
                    Show = message.NewValue;
                    break;
                case nameof(SettingsViewModel.ShowLinkshell8Channel):
                    if (recipientChannel.Code != ChatCode.LinkShell8) break;
                    Show = message.NewValue;
                    break;
                case nameof(SettingsViewModel.ShowCwLinkshell1Channel):
                    if (recipientChannel.Code != ChatCode.CWLinkShell1) break;
                    Show = message.NewValue;
                    break;
                case nameof(SettingsViewModel.ShowCwLinkshell2Channel):
                    if (recipientChannel.Code != ChatCode.CWLinkShell2) break;
                    Show = message.NewValue;
                    break;
                case nameof(SettingsViewModel.ShowCwLinkshell3Channel):
                    if (recipientChannel.Code != ChatCode.CWLinkShell3) break;
                    Show = message.NewValue;
                    break;
                case nameof(SettingsViewModel.ShowCwLinkshell4Channel):
                    if (recipientChannel.Code != ChatCode.CWLinkShell4) break;
                    Show = message.NewValue;
                    break;
                case nameof(SettingsViewModel.ShowCwLinkshell5Channel):
                    if (recipientChannel.Code != ChatCode.CWLinkShell5) break;
                    Show = message.NewValue;
                    break;
                case nameof(SettingsViewModel.ShowCwLinkshell6Channel):
                    if (recipientChannel.Code != ChatCode.CWLinkShell6) break;
                    Show = message.NewValue;
                    break;
                case nameof(SettingsViewModel.ShowCwLinkshell7Channel):
                    if (recipientChannel.Code != ChatCode.CWLinkShell7) break;
                    Show = message.NewValue;
                    break;
                case nameof(SettingsViewModel.ShowCwLinkshell8Channel):
                    if (recipientChannel.Code != ChatCode.CWLinkShell8) break;
                    Show = message.NewValue;
                    break;
                case nameof(SettingsViewModel.ShowGameSystemChannel):
                    if (recipientChannel.Code != ChatCode.System) break;
                    Show = message.NewValue;
                    break;
                case nameof(SettingsViewModel.ShowGameNoticeChannel):
                    if (recipientChannel.Code != ChatCode.Notice) break;
                    Show = message.NewValue;
                    break;
                case nameof(SettingsViewModel.ShowGameErrorChannel):
                    if (recipientChannel.Code != ChatCode.Error) break;
                    Show = message.NewValue;
                    break;
                case nameof(SettingsViewModel.ShowGatherChannel):
                    if (recipientChannel.Code != ChatCode.Gather) break;
                    Show = message.NewValue;
                    break;
                case nameof(SettingsViewModel.ShowGilReceiveChannel):
                    if (recipientChannel.Code != ChatCode.GilReceive) break;
                    Show = message.NewValue;
                    break;
                case nameof(SettingsViewModel.ShowMarketSoldChannel):
                    if (recipientChannel.Code != ChatCode.MarketSold) break;
                    Show = message.NewValue;
                    break;
                case nameof(SettingsViewModel.ShowRecruitmentChannel):
                    if (recipientChannel.Code != ChatCode.Recruitment) break;
                    Show = message.NewValue;
                    break;
                case nameof(SettingsViewModel.ShowNpcDialogChannel):
                    if (recipientChannel.Code != ChatCode.NPCDialog) break;
                    Show = message.NewValue;
                    break;
                case nameof(SettingsViewModel.ShowNpcAnnounceChannel):
                    if (recipientChannel.Code != ChatCode.NPCAnnounce) break;
                    Show = message.NewValue;
                    break;
                case nameof(SettingsViewModel.ShowBossQuotesChannel):
                    if (recipientChannel.Code != ChatCode.BossQuotes) break;
                    Show = message.NewValue;
                    break;
                default:
                    break;
            }
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

            switch (message.PropertyName)
            {
                //case nameof(SettingsViewModel.GroupPartyFieldLanguage):
                //    if (recipientChannel.Code != ChatCode.GroupPartyField) break;
                //    MajorLanguage = message.NewValue;
                //    break;
                case nameof(SettingsViewModel.EchoLanguage):
                    if (recipientChannel.Code != ChatCode.Echo) break;
                    MajorLanguage = message.NewValue;
                    break;
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
