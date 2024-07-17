using IronworksTranslator.Models.Enums;
using YamlDotNet.Serialization;

namespace IronworksTranslator.Models.Settings
{
#pragma warning disable CS8618
    public partial class ChannelSettings : ObservableRecipient
    {
        /*
         * Echo, Say, Yell, Shout, Tell, Party, Alliance, Emote, EmoteCustom
         * LinkShell1, LinkShell2, LinkShell3, LinkShell4, LinkShell5, LinkShell6, LinkShell7, LinkShell8,
         * CWLinkShell1, CWLinkShell2, CWLinkShell3, CWLinkShell4, CWLinkShell5, CWLinkShell6, CWLinkShell7, CWLinkShell8,
         * FreeCompany, Novice, System, Notice, Error, Gather, GilReceive, NPCDialog, NPCAnnounce, MarketSold, Recruitment,
         * BossQuotes
         */
        public ChannelSettings()
        {
            InitChannels();
        }

        private void InitChannels()
        {
            Echo = new ChatChannel { Code = ChatCode.Echo, IsActive = true, Show = true, MajorLanguage = ClientLanguage.Japanese };
            Say = new ChatChannel { Code = ChatCode.Say, IsActive = true, Show = true, MajorLanguage = ClientLanguage.Japanese };
            Yell = new ChatChannel { Code = ChatCode.Yell, IsActive = true, Show = true, MajorLanguage = ClientLanguage.Japanese };
            Shout = new ChatChannel { Code = ChatCode.Shout, IsActive = true, Show = true, MajorLanguage = ClientLanguage.Japanese };
            Tell = new ChatChannel { Code = ChatCode.Tell, IsActive = true, Show = true, MajorLanguage = ClientLanguage.Japanese };
            Party = new ChatChannel { Code = ChatCode.Party, IsActive = true, Show = true, MajorLanguage = ClientLanguage.Japanese };
            Alliance = new ChatChannel { Code = ChatCode.Alliance, IsActive = true, Show = true, MajorLanguage = ClientLanguage.Japanese };

            Linkshell1 = new ChatChannel { Code = ChatCode.LinkShell1, IsActive = true, Show = true, MajorLanguage = ClientLanguage.Japanese };
            Linkshell2 = new ChatChannel { Code = ChatCode.LinkShell2, IsActive = true, Show = true, MajorLanguage = ClientLanguage.Japanese };
            Linkshell3 = new ChatChannel { Code = ChatCode.LinkShell3, IsActive = true, Show = true, MajorLanguage = ClientLanguage.Japanese };
            Linkshell4 = new ChatChannel { Code = ChatCode.LinkShell4, IsActive = true, Show = true, MajorLanguage = ClientLanguage.Japanese };
            Linkshell5 = new ChatChannel { Code = ChatCode.LinkShell5, IsActive = true, Show = true, MajorLanguage = ClientLanguage.Japanese };
            Linkshell6 = new ChatChannel { Code = ChatCode.LinkShell6, IsActive = true, Show = true, MajorLanguage = ClientLanguage.Japanese };
            Linkshell7 = new ChatChannel { Code = ChatCode.LinkShell7, IsActive = true, Show = true, MajorLanguage = ClientLanguage.Japanese };
            Linkshell8 = new ChatChannel { Code = ChatCode.LinkShell8, IsActive = true, Show = true, MajorLanguage = ClientLanguage.Japanese };

            CwLinkshell1 = new ChatChannel { Code = ChatCode.CWLinkShell1, IsActive = true, Show = true, MajorLanguage = ClientLanguage.Japanese };
            CwLinkshell2 = new ChatChannel { Code = ChatCode.CWLinkShell2, IsActive = true, Show = true, MajorLanguage = ClientLanguage.Japanese };
            CwLinkshell3 = new ChatChannel { Code = ChatCode.CWLinkShell3, IsActive = true, Show = true, MajorLanguage = ClientLanguage.Japanese };
            CwLinkshell4 = new ChatChannel { Code = ChatCode.CWLinkShell4, IsActive = true, Show = true, MajorLanguage = ClientLanguage.Japanese };
            CwLinkshell5 = new ChatChannel { Code = ChatCode.CWLinkShell5, IsActive = true, Show = true, MajorLanguage = ClientLanguage.Japanese };
            CwLinkshell6 = new ChatChannel { Code = ChatCode.CWLinkShell6, IsActive = true, Show = true, MajorLanguage = ClientLanguage.Japanese };
            CwLinkshell7 = new ChatChannel { Code = ChatCode.CWLinkShell7, IsActive = true, Show = true, MajorLanguage = ClientLanguage.Japanese };
            CwLinkshell8 = new ChatChannel { Code = ChatCode.CWLinkShell8, IsActive = true, Show = true, MajorLanguage = ClientLanguage.Japanese };

            Freecompany = new ChatChannel { Code = ChatCode.FreeCompany, IsActive = true, Show = true, MajorLanguage = ClientLanguage.Japanese };
            Novice = new ChatChannel { Code = ChatCode.Novice, IsActive = true, Show = true, MajorLanguage = ClientLanguage.Japanese };
            Emote = new ChatChannel { Code = ChatCode.Emote, IsActive = true, Show = true, MajorLanguage = ClientLanguage.Japanese };
            EmoteCustom = new ChatChannel { Code = ChatCode.EmoteCustom, IsActive = true, Show = true, MajorLanguage = ClientLanguage.Japanese };
            System = new ChatChannel { Code = ChatCode.System, IsActive = true, Show = true, MajorLanguage = ClientLanguage.Japanese };
            Gather = new ChatChannel { Code = ChatCode.Gather, IsActive = true, Show = true, MajorLanguage = ClientLanguage.Japanese };
            Notice = new ChatChannel { Code = ChatCode.Notice, IsActive = true, Show = true, MajorLanguage = ClientLanguage.Japanese };
            Error = new ChatChannel { Code = ChatCode.Error, IsActive = true, Show = true, MajorLanguage = ClientLanguage.Japanese };
            GilReceive = new ChatChannel { Code = ChatCode.GilReceive, IsActive = true, Show = true, MajorLanguage = ClientLanguage.Japanese };
            NpcDialog = new ChatChannel { Code = ChatCode.NPCDialog, IsActive = true, Show = true, MajorLanguage = ClientLanguage.Japanese };
            NpcAnnounce = new ChatChannel { Code = ChatCode.NPCAnnounce, IsActive = true, Show = true, MajorLanguage = ClientLanguage.Japanese };
            MarketSold = new ChatChannel { Code = ChatCode.MarketSold, IsActive = true, Show = true, MajorLanguage = ClientLanguage.Japanese };
            Recruitment = new ChatChannel { Code = ChatCode.Recruitment, IsActive = true, Show = true, MajorLanguage = ClientLanguage.Japanese };
            BossQuotes = new ChatChannel { Code = ChatCode.BossQuotes, IsActive = true, Show = true, MajorLanguage = ClientLanguage.Japanese };
        }

        [ObservableProperty]
        [property: YamlMember(Alias = "echo")]
        private ChatChannel _echo;

        [ObservableProperty]
        [property: YamlMember(Alias = "say")]
        private ChatChannel _say;

        [ObservableProperty]
        [property: YamlMember(Alias = "yell")]
        private ChatChannel _yell;

        [ObservableProperty]
        [property: YamlMember(Alias = "shout")]
        private ChatChannel _shout;

        [ObservableProperty]
        [property: YamlMember(Alias = "tell")]
        private ChatChannel _tell;

        [ObservableProperty]
        [property: YamlMember(Alias = "party")]
        private ChatChannel _party;

        [ObservableProperty]
        [property: YamlMember(Alias = "alliance")]
        private ChatChannel _alliance;

        [ObservableProperty]
        [property: YamlMember(Alias = "linkshell1")]
        private ChatChannel _linkshell1;

        [ObservableProperty]
        [property: YamlMember(Alias = "linkshell2")]
        private ChatChannel _linkshell2;

        [ObservableProperty]
        [property: YamlMember(Alias = "linkshell3")]
        private ChatChannel _linkshell3;

        [ObservableProperty]
        [property: YamlMember(Alias = "linkshell4")]
        private ChatChannel _linkshell4;

        [ObservableProperty]
        [property: YamlMember(Alias = "linkshell5")]
        private ChatChannel _linkshell5;

        [ObservableProperty]
        [property: YamlMember(Alias = "linkshell6")]
        private ChatChannel _linkshell6;

        [ObservableProperty]
        [property: YamlMember(Alias = "linkshell7")]
        private ChatChannel _linkshell7;

        [ObservableProperty]
        [property: YamlMember(Alias = "linkshell8")]
        private ChatChannel _linkshell8;

        [ObservableProperty]
        [property: YamlMember(Alias = "cwLinkshell1")]
        private ChatChannel _cwLinkshell1;

        [ObservableProperty]
        [property: YamlMember(Alias = "cwLinkshell2")]
        private ChatChannel _cwLinkshell2;

        [ObservableProperty]
        [property: YamlMember(Alias = "cwLinkshell3")]
        private ChatChannel _cwLinkshell3;

        [ObservableProperty]
        [property: YamlMember(Alias = "cwLinkshell4")]
        private ChatChannel _cwLinkshell4;

        [ObservableProperty]
        [property: YamlMember(Alias = "cwLinkshell5")]
        private ChatChannel _cwLinkshell5;

        [ObservableProperty]
        [property: YamlMember(Alias = "cwLinkshell6")]
        private ChatChannel _cwLinkshell6;

        [ObservableProperty]
        [property: YamlMember(Alias = "cwLinkshell7")]
        private ChatChannel _cwLinkshell7;

        [ObservableProperty]
        [property: YamlMember(Alias = "cwLinkshell8")]
        private ChatChannel _cwLinkshell8;

        [ObservableProperty]
        [property: YamlMember(Alias = "freecompany")]
        private ChatChannel _freecompany;

        [ObservableProperty]
        [property: YamlMember(Alias = "novice")]
        private ChatChannel _novice;

        [ObservableProperty]
        [property: YamlMember(Alias = "emote")]
        private ChatChannel _emote;

        [ObservableProperty]
        [property: YamlMember(Alias = "emote_custom")]
        private ChatChannel _emoteCustom;

        [ObservableProperty]
        [property: YamlMember(Alias = "system")]
        private ChatChannel _system;

        [ObservableProperty]
        [property: YamlMember(Alias = "gather")]
        private ChatChannel _gather;

        [ObservableProperty]
        [property: YamlMember(Alias = "notice")]
        private ChatChannel _notice;

        [ObservableProperty]
        [property: YamlMember(Alias = "error")]
        private ChatChannel _error;

        [ObservableProperty]
        [property: YamlMember(Alias = "gil_receive")]
        private ChatChannel _gilReceive;

        [ObservableProperty]
        [property: YamlMember(Alias = "npc_dialog")]
        private ChatChannel _npcDialog;

        [ObservableProperty]
        [property: YamlMember(Alias = "npc_announce")]
        private ChatChannel _npcAnnounce;

        [ObservableProperty]
        [property: YamlMember(Alias = "market_sold")]
        private ChatChannel _marketSold;

        [ObservableProperty]
        [property: YamlMember(Alias = "recruitment")]
        private ChatChannel _recruitment;

        [ObservableProperty]
        [property: YamlMember(Alias = "boss_quotes")]
        private ChatChannel _bossQuotes;
    }
#pragma warning restore CS8618
}
