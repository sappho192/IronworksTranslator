using IronworksTranslator.Models;
using IronworksTranslator.Models.Enums;
using IronworksTranslator.Models.Settings;
using Wpf.Ui.Controls;

namespace IronworksTranslator.ViewModels.Pages
{
#pragma warning disable CS8602
    public partial class SettingsViewModel : ObservableRecipient, INavigationAware
    {
        /*
            [ObservableProperty]
            [NotifyPropertyChangedRecipients]
            private ApplicationTheme _currentTheme = IronworksSettings.Instance.UiSettings.Theme;
         */

        #region Party & Field
        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        [NotifyPropertyChangedFor(nameof(SayLanguageIndex))]
        private ClientLanguage _sayLanguage = IronworksSettings.Instance.ChannelSettings.Say.MajorLanguage;
        [ObservableProperty]
        private int _sayLanguageIndex = (int)IronworksSettings.Instance.ChannelSettings.Say.MajorLanguage;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        [NotifyPropertyChangedFor(nameof(YellLanguageIndex))]
        private ClientLanguage _yellLanguage = IronworksSettings.Instance.ChannelSettings.Yell.MajorLanguage;
        [ObservableProperty]
        private int _yellLanguageIndex = (int)IronworksSettings.Instance.ChannelSettings.Yell.MajorLanguage;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        [NotifyPropertyChangedFor(nameof(ShoutLanguageIndex))]
        private ClientLanguage _shoutLanguage = IronworksSettings.Instance.ChannelSettings.Shout.MajorLanguage;
        [ObservableProperty]
        private int _shoutLanguageIndex = (int)IronworksSettings.Instance.ChannelSettings.Shout.MajorLanguage;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        [NotifyPropertyChangedFor(nameof(TellLanguageIndex))]
        private ClientLanguage _tellLanguage = IronworksSettings.Instance.ChannelSettings.Tell.MajorLanguage;
        [ObservableProperty]
        private int _tellLanguageIndex = (int)IronworksSettings.Instance.ChannelSettings.Tell.MajorLanguage;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        [NotifyPropertyChangedFor(nameof(PartyLanguageIndex))]
        private ClientLanguage _partyLanguage = IronworksSettings.Instance.ChannelSettings.Party.MajorLanguage;
        [ObservableProperty]
        private int _partyLanguageIndex = (int)IronworksSettings.Instance.ChannelSettings.Party.MajorLanguage;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        [NotifyPropertyChangedFor(nameof(AllianceLanguageIndex))]
        private ClientLanguage _allianceLanguage = IronworksSettings.Instance.ChannelSettings.Alliance.MajorLanguage;
        [ObservableProperty]
        private int _allianceLanguageIndex = (int)IronworksSettings.Instance.ChannelSettings.Alliance.MajorLanguage;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        [NotifyPropertyChangedFor(nameof(EmoteLanguageIndex))]
        private ClientLanguage _emoteLanguage = IronworksSettings.Instance.ChannelSettings.Emote.MajorLanguage;
        [ObservableProperty]
        private int _emoteLanguageIndex = (int)IronworksSettings.Instance.ChannelSettings.Emote.MajorLanguage;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        [NotifyPropertyChangedFor(nameof(EmoteCustomLanguageIndex))]
        private ClientLanguage _emoteCustomLanguage = IronworksSettings.Instance.ChannelSettings.EmoteCustom.MajorLanguage;
        [ObservableProperty]
        private int _emoteCustomLanguageIndex = (int)IronworksSettings.Instance.ChannelSettings.EmoteCustom.MajorLanguage;

        #endregion

        #region Linkshells
        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        [NotifyPropertyChangedFor(nameof(Linkshell1LanguageIndex))]
        private ClientLanguage _linkshell1Language = IronworksSettings.Instance.ChannelSettings.Linkshell1.MajorLanguage;
        [ObservableProperty]
        private int _linkshell1LanguageIndex = (int)IronworksSettings.Instance.ChannelSettings.Linkshell1.MajorLanguage;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        [NotifyPropertyChangedFor(nameof(Linkshell2LanguageIndex))]
        private ClientLanguage _linkshell2Language = IronworksSettings.Instance.ChannelSettings.Linkshell2.MajorLanguage;
        [ObservableProperty]
        private int _linkshell2LanguageIndex = (int)IronworksSettings.Instance.ChannelSettings.Linkshell2.MajorLanguage;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        [NotifyPropertyChangedFor(nameof(Linkshell3LanguageIndex))]
        private ClientLanguage _linkshell3Language = IronworksSettings.Instance.ChannelSettings.Linkshell3.MajorLanguage;
        [ObservableProperty]
        private int _linkshell3LanguageIndex = (int)IronworksSettings.Instance.ChannelSettings.Linkshell3.MajorLanguage;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        [NotifyPropertyChangedFor(nameof(Linkshell4LanguageIndex))]
        private ClientLanguage _linkshell4Language = IronworksSettings.Instance.ChannelSettings.Linkshell4.MajorLanguage;
        [ObservableProperty]
        private int _linkshell4LanguageIndex = (int)IronworksSettings.Instance.ChannelSettings.Linkshell4.MajorLanguage;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        [NotifyPropertyChangedFor(nameof(Linkshell5LanguageIndex))]
        private ClientLanguage _linkshell5Language = IronworksSettings.Instance.ChannelSettings.Linkshell5.MajorLanguage;
        [ObservableProperty]
        private int _linkshell5LanguageIndex = (int)IronworksSettings.Instance.ChannelSettings.Linkshell5.MajorLanguage;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        [NotifyPropertyChangedFor(nameof(Linkshell6LanguageIndex))]
        private ClientLanguage _linkshell6Language = IronworksSettings.Instance.ChannelSettings.Linkshell6.MajorLanguage;
        [ObservableProperty]
        private int _linkshell6LanguageIndex = (int)IronworksSettings.Instance.ChannelSettings.Linkshell6.MajorLanguage;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        [NotifyPropertyChangedFor(nameof(Linkshell7LanguageIndex))]
        private ClientLanguage _linkshell7Language = IronworksSettings.Instance.ChannelSettings.Linkshell7.MajorLanguage;
        [ObservableProperty]
        private int _linkshell7LanguageIndex = (int)IronworksSettings.Instance.ChannelSettings.Linkshell7.MajorLanguage;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        [NotifyPropertyChangedFor(nameof(Linkshell8LanguageIndex))]
        private ClientLanguage _linkshell8Language = IronworksSettings.Instance.ChannelSettings.Linkshell8.MajorLanguage;
        [ObservableProperty]
        private int _linkshell8LanguageIndex = (int)IronworksSettings.Instance.ChannelSettings.Linkshell8.MajorLanguage;
        #endregion

        #region CrossWorld Linkshells
        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        [NotifyPropertyChangedFor(nameof(CwLinkshell1LanguageIndex))]
        private ClientLanguage _cwLinkshell1Language = IronworksSettings.Instance.ChannelSettings.CwLinkshell1.MajorLanguage;
        [ObservableProperty]
        private int _cwLinkshell1LanguageIndex = (int)IronworksSettings.Instance.ChannelSettings.CwLinkshell1.MajorLanguage;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        [NotifyPropertyChangedFor(nameof(CwLinkshell2LanguageIndex))]
        private ClientLanguage _cwLinkshell2Language = IronworksSettings.Instance.ChannelSettings.CwLinkshell2.MajorLanguage;
        [ObservableProperty]
        private int _cwLinkshell2LanguageIndex = (int)IronworksSettings.Instance.ChannelSettings.CwLinkshell2.MajorLanguage;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        [NotifyPropertyChangedFor(nameof(CwLinkshell3LanguageIndex))]
        private ClientLanguage _cwLinkshell3Language = IronworksSettings.Instance.ChannelSettings.CwLinkshell3.MajorLanguage;
        [ObservableProperty]
        private int _cwLinkshell3LanguageIndex = (int)IronworksSettings.Instance.ChannelSettings.CwLinkshell3.MajorLanguage;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        [NotifyPropertyChangedFor(nameof(CwLinkshell4LanguageIndex))]
        private ClientLanguage _cwLinkshell4Language = IronworksSettings.Instance.ChannelSettings.CwLinkshell4.MajorLanguage;
        [ObservableProperty]
        private int _cwLinkshell4LanguageIndex = (int)IronworksSettings.Instance.ChannelSettings.CwLinkshell4.MajorLanguage;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        [NotifyPropertyChangedFor(nameof(CwLinkshell5LanguageIndex))]
        private ClientLanguage _cwLinkshell5Language = IronworksSettings.Instance.ChannelSettings.CwLinkshell5.MajorLanguage;
        [ObservableProperty]
        private int _cwLinkshell5LanguageIndex = (int)IronworksSettings.Instance.ChannelSettings.CwLinkshell5.MajorLanguage;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        [NotifyPropertyChangedFor(nameof(CwLinkshell6LanguageIndex))]
        private ClientLanguage _cwLinkshell6Language = IronworksSettings.Instance.ChannelSettings.CwLinkshell6.MajorLanguage;
        [ObservableProperty]
        private int _cwLinkshell6LanguageIndex = (int)IronworksSettings.Instance.ChannelSettings.CwLinkshell6.MajorLanguage;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        [NotifyPropertyChangedFor(nameof(CwLinkshell7LanguageIndex))]
        private ClientLanguage _cwLinkshell7Language = IronworksSettings.Instance.ChannelSettings.CwLinkshell7.MajorLanguage;
        [ObservableProperty]
        private int _cwLinkshell7LanguageIndex = (int)IronworksSettings.Instance.ChannelSettings.CwLinkshell7.MajorLanguage;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        [NotifyPropertyChangedFor(nameof(CwLinkshell8LanguageIndex))]
        private ClientLanguage _cwLinkshell8Language = IronworksSettings.Instance.ChannelSettings.CwLinkshell8.MajorLanguage;
        [ObservableProperty]
        private int _cwLinkshell8LanguageIndex = (int)IronworksSettings.Instance.ChannelSettings.CwLinkshell8.MajorLanguage;
        #endregion

        #region Community
        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        [NotifyPropertyChangedFor(nameof(FreecompanyLanguageIndex))]
        private ClientLanguage _freecompanyLanguage = IronworksSettings.Instance.ChannelSettings.Freecompany.MajorLanguage;
        [ObservableProperty]
        private int _freecompanyLanguageIndex = (int)IronworksSettings.Instance.ChannelSettings.Freecompany.MajorLanguage;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        [NotifyPropertyChangedFor(nameof(NoviceLanguageIndex))]
        private ClientLanguage _noviceLanguage = IronworksSettings.Instance.ChannelSettings.Novice.MajorLanguage;
        [ObservableProperty]
        private int _noviceLanguageIndex = (int)IronworksSettings.Instance.ChannelSettings.Novice.MajorLanguage;
        #endregion

        #region System
        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        [NotifyPropertyChangedFor(nameof(GameSystemLanguageIndex))]
        private ClientLanguage _gameSystemLanguage = IronworksSettings.Instance.ChannelSettings.System.MajorLanguage;
        [ObservableProperty]
        private int _gameSystemLanguageIndex = (int)IronworksSettings.Instance.ChannelSettings.System.MajorLanguage;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        [NotifyPropertyChangedFor(nameof(GatherLanguageIndex))]
        private ClientLanguage _gatherLanguage = IronworksSettings.Instance.ChannelSettings.Gather.MajorLanguage;
        [ObservableProperty]
        private int _gatherLanguageIndex = (int)IronworksSettings.Instance.ChannelSettings.Gather.MajorLanguage;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        [NotifyPropertyChangedFor(nameof(GameNoticeLanguageIndex))]
        private ClientLanguage _gameNoticeLanguage = IronworksSettings.Instance.ChannelSettings.Notice.MajorLanguage;
        [ObservableProperty]
        private int _gameNoticeLanguageIndex = (int)IronworksSettings.Instance.ChannelSettings.Notice.MajorLanguage;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        [NotifyPropertyChangedFor(nameof(GameErrorLanguageIndex))]
        private ClientLanguage _gameErrorLanguage = IronworksSettings.Instance.ChannelSettings.Error.MajorLanguage;
        [ObservableProperty]
        private int _gameErrorLanguageIndex = (int)IronworksSettings.Instance.ChannelSettings.Error.MajorLanguage;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        [NotifyPropertyChangedFor(nameof(GilReceiveLanguageIndex))]
        private ClientLanguage _gilReceiveLanguage = IronworksSettings.Instance.ChannelSettings.GilReceive.MajorLanguage;
        [ObservableProperty]
        private int _gilReceiveLanguageIndex = (int)IronworksSettings.Instance.ChannelSettings.GilReceive.MajorLanguage;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        [NotifyPropertyChangedFor(nameof(NpcDialogLanguageIndex))]
        private ClientLanguage _npcDialogLanguage = IronworksSettings.Instance.ChannelSettings.NpcDialog.MajorLanguage;
        [ObservableProperty]
        private int _npcDialogLanguageIndex = (int)IronworksSettings.Instance.ChannelSettings.NpcDialog.MajorLanguage;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        [NotifyPropertyChangedFor(nameof(NpcAnnounceLanguageIndex))]
        private ClientLanguage _npcAnnounceLanguage = IronworksSettings.Instance.ChannelSettings.NpcAnnounce.MajorLanguage;
        [ObservableProperty]
        private int _npcAnnounceLanguageIndex = (int)IronworksSettings.Instance.ChannelSettings.NpcAnnounce.MajorLanguage;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        [NotifyPropertyChangedFor(nameof(MarketSoldLanguageIndex))]
        private ClientLanguage _marketSoldLanguage = IronworksSettings.Instance.ChannelSettings.MarketSold.MajorLanguage;
        [ObservableProperty]
        private int _marketSoldLanguageIndex = (int)IronworksSettings.Instance.ChannelSettings.MarketSold.MajorLanguage;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        [NotifyPropertyChangedFor(nameof(RecruitmentLanguageIndex))]
        private ClientLanguage _recruitmentLanguage = IronworksSettings.Instance.ChannelSettings.Recruitment.MajorLanguage;
        [ObservableProperty]
        private int _recruitmentLanguageIndex = (int)IronworksSettings.Instance.ChannelSettings.Recruitment.MajorLanguage;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        [NotifyPropertyChangedFor(nameof(BossQuotesLanguageIndex))]
        private ClientLanguage _bossQuotesLanguage = IronworksSettings.Instance.ChannelSettings.BossQuotes.MajorLanguage;
        [ObservableProperty]
        private int _bossQuotesLanguageIndex = (int)IronworksSettings.Instance.ChannelSettings.BossQuotes.MajorLanguage;
        #endregion
    }
#pragma warning restore CS8602
}
