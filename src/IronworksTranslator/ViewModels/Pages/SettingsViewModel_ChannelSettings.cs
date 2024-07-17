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

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private ChatChannel _linkshell1 = IronworksSettings.Instance.ChannelSettings.Linkshell1;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private ChatChannel _linkshell2 = IronworksSettings.Instance.ChannelSettings.Linkshell2;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private ChatChannel _linkshell3 = IronworksSettings.Instance.ChannelSettings.Linkshell3;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private ChatChannel _linkshell4 = IronworksSettings.Instance.ChannelSettings.Linkshell4;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private ChatChannel _linkshell5 = IronworksSettings.Instance.ChannelSettings.Linkshell5;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private ChatChannel _linkshell6 = IronworksSettings.Instance.ChannelSettings.Linkshell6;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private ChatChannel _linkshell7 = IronworksSettings.Instance.ChannelSettings.Linkshell7;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private ChatChannel _linkshell8 = IronworksSettings.Instance.ChannelSettings.Linkshell8;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private ChatChannel _cwLinkshell1 = IronworksSettings.Instance.ChannelSettings.CwLinkshell1;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private ChatChannel _cwLinkshell2 = IronworksSettings.Instance.ChannelSettings.CwLinkshell2;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private ChatChannel _cwLinkshell3 = IronworksSettings.Instance.ChannelSettings.CwLinkshell3;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private ChatChannel _cwLinkshell4 = IronworksSettings.Instance.ChannelSettings.CwLinkshell4;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private ChatChannel _cwLinkshell5 = IronworksSettings.Instance.ChannelSettings.CwLinkshell5;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private ChatChannel _cwLinkshell6 = IronworksSettings.Instance.ChannelSettings.CwLinkshell6;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private ChatChannel _cwLinkshell7 = IronworksSettings.Instance.ChannelSettings.CwLinkshell7;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private ChatChannel _cwLinkshell8 = IronworksSettings.Instance.ChannelSettings.CwLinkshell8;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private ChatChannel _freecompany = IronworksSettings.Instance.ChannelSettings.Freecompany;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private ChatChannel _novice = IronworksSettings.Instance.ChannelSettings.Novice;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private ChatChannel _gameSystem = IronworksSettings.Instance.ChannelSettings.System;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private ChatChannel _gather = IronworksSettings.Instance.ChannelSettings.Gather;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private ChatChannel _gameNotice = IronworksSettings.Instance.ChannelSettings.Notice;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private ChatChannel _gameError = IronworksSettings.Instance.ChannelSettings.Error;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private ChatChannel _gilReceive = IronworksSettings.Instance.ChannelSettings.GilReceive;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private ChatChannel _npcDialog = IronworksSettings.Instance.ChannelSettings.NpcDialog;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private ChatChannel _npcAnnounce = IronworksSettings.Instance.ChannelSettings.NpcAnnounce;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private ChatChannel _marketSold = IronworksSettings.Instance.ChannelSettings.MarketSold;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private ChatChannel _recruitment = IronworksSettings.Instance.ChannelSettings.Recruitment;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private ChatChannel _bossQuotes = IronworksSettings.Instance.ChannelSettings.BossQuotes;
    }
#pragma warning restore CS8602
}
