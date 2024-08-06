using IronworksTranslator.Models.Enums;
using YamlDotNet.Serialization;

namespace IronworksTranslator.Models.Settings
{
#pragma warning disable CS8618
    public partial class ChannelSettings : ObservableRecipient
    {
        /*
         * Say, Yell, Shout, Tell, Party, Alliance, Emote, EmoteCustom
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
            GroupPartyField = new ChatChannel { Code = ChatCode.GroupPartyField, Show = true, MajorLanguage = ClientLanguage.Japanese, Color = "#FFFFFF" };
            Say = new ChatChannel { Code = ChatCode.Say, Show = true, MajorLanguage = ClientLanguage.Japanese, Color = "#F7F7F7" };
            Yell = new ChatChannel { Code = ChatCode.Yell, Show = true, MajorLanguage = ClientLanguage.Japanese, Color = "Yellow" };
            Shout = new ChatChannel { Code = ChatCode.Shout, Show = true, MajorLanguage = ClientLanguage.Japanese, Color = "#FFA666" };
            Tell = new ChatChannel { Code = ChatCode.Tell, Show = true, MajorLanguage = ClientLanguage.Japanese, Color = "#FFB8DE" };
            Party = new ChatChannel { Code = ChatCode.Party, Show = true, MajorLanguage = ClientLanguage.Japanese, Color = "#66E5FF" };
            Alliance = new ChatChannel { Code = ChatCode.Alliance, Show = true, MajorLanguage = ClientLanguage.Japanese, Color = "#FF8000" };
            Emote = new ChatChannel { Code = ChatCode.Emote, Show = true, MajorLanguage = ClientLanguage.Japanese, Color = "#BAFFF0" };
            EmoteCustom = new ChatChannel { Code = ChatCode.EmoteCustom, Show = true, MajorLanguage = ClientLanguage.Japanese, Color = "#BAFFF0" };

            GroupCommunity = new ChatChannel { Code = ChatCode.GroupCommunity, Show = true, MajorLanguage = ClientLanguage.Japanese, Color = "#FFFFFF" };
            Freecompany = new ChatChannel { Code = ChatCode.FreeCompany, Show = true, MajorLanguage = ClientLanguage.Japanese, Color = "#ABDBE5" };
            Novice = new ChatChannel { Code = ChatCode.Novice, Show = true, MajorLanguage = ClientLanguage.Japanese, Color = "#D4FF7D" };

            GroupLinkshell = new ChatChannel { Code = ChatCode.GroupLinkShell, Show = true, MajorLanguage = ClientLanguage.Japanese, Color = "#D4FF7D" };
            Linkshell1 = new ChatChannel { Code = ChatCode.LinkShell1, Show = true, MajorLanguage = ClientLanguage.Japanese, Color = "#D4FF7D" };
            Linkshell2 = new ChatChannel { Code = ChatCode.LinkShell2, Show = true, MajorLanguage = ClientLanguage.Japanese, Color = "#D4FF7D" };
            Linkshell3 = new ChatChannel { Code = ChatCode.LinkShell3, Show = true, MajorLanguage = ClientLanguage.Japanese, Color = "#D4FF7D" };
            Linkshell4 = new ChatChannel { Code = ChatCode.LinkShell4, Show = true, MajorLanguage = ClientLanguage.Japanese, Color = "#D4FF7D" };
            Linkshell5 = new ChatChannel { Code = ChatCode.LinkShell5, Show = true, MajorLanguage = ClientLanguage.Japanese, Color = "#D4FF7D" };
            Linkshell6 = new ChatChannel { Code = ChatCode.LinkShell6, Show = true, MajorLanguage = ClientLanguage.Japanese, Color = "#D4FF7D" };
            Linkshell7 = new ChatChannel { Code = ChatCode.LinkShell7, Show = true, MajorLanguage = ClientLanguage.Japanese, Color = "#D4FF7D" };
            Linkshell8 = new ChatChannel { Code = ChatCode.LinkShell8, Show = true, MajorLanguage = ClientLanguage.Japanese, Color = "#D4FF7D" };

            GroupCwLinkshell = new ChatChannel { Code = ChatCode.GroupCWLinkShell, Show = true, MajorLanguage = ClientLanguage.Japanese, Color = "#D4FF7D" };
            CwLinkshell1 = new ChatChannel { Code = ChatCode.CWLinkShell1, Show = true, MajorLanguage = ClientLanguage.Japanese, Color = "#D4FF7D" };
            CwLinkshell2 = new ChatChannel { Code = ChatCode.CWLinkShell2, Show = true, MajorLanguage = ClientLanguage.Japanese, Color = "#D4FF7D" };
            CwLinkshell3 = new ChatChannel { Code = ChatCode.CWLinkShell3, Show = true, MajorLanguage = ClientLanguage.Japanese, Color = "#D4FF7D" };
            CwLinkshell4 = new ChatChannel { Code = ChatCode.CWLinkShell4, Show = true, MajorLanguage = ClientLanguage.Japanese, Color = "#D4FF7D" };
            CwLinkshell5 = new ChatChannel { Code = ChatCode.CWLinkShell5, Show = true, MajorLanguage = ClientLanguage.Japanese, Color = "#D4FF7D" };
            CwLinkshell6 = new ChatChannel { Code = ChatCode.CWLinkShell6, Show = true, MajorLanguage = ClientLanguage.Japanese, Color = "#D4FF7D" };
            CwLinkshell7 = new ChatChannel { Code = ChatCode.CWLinkShell7, Show = true, MajorLanguage = ClientLanguage.Japanese, Color = "#D4FF7D" };
            CwLinkshell8 = new ChatChannel { Code = ChatCode.CWLinkShell8, Show = true, MajorLanguage = ClientLanguage.Japanese, Color = "#D4FF7D" };

            GroupSystem = new ChatChannel { Code = ChatCode.GroupSystem, Show = true, MajorLanguage = ClientLanguage.Japanese, Color = "#CCCCCC" };
            Notice = new ChatChannel { Code = ChatCode.Notice, Show = true, MajorLanguage = ClientLanguage.Japanese, Color = "#D4FF7D" };
            System = new ChatChannel { Code = ChatCode.System, Show = true, MajorLanguage = ClientLanguage.Japanese, Color = "#CCCCCC" };
            Error = new ChatChannel { Code = ChatCode.Error, Show = true, MajorLanguage = ClientLanguage.Japanese, Color = "#FFA666" };
            NpcDialog = new ChatChannel { Code = ChatCode.NPCDialog, Show = true, MajorLanguage = ClientLanguage.Japanese, Color = "#ABD647" };
            NpcAnnounce = new ChatChannel { Code = ChatCode.NPCAnnounce, Show = true, MajorLanguage = ClientLanguage.Japanese, Color = "#ABD647" };
            BossQuotes = new ChatChannel { Code = ChatCode.BossQuotes, Show = true, MajorLanguage = ClientLanguage.Japanese, Color = "Fuchsia" };
            Recruitment = new ChatChannel { Code = ChatCode.Recruitment, Show = true, MajorLanguage = ClientLanguage.Japanese, Color = "#CCCCCC" };
            Gather = new ChatChannel { Code = ChatCode.Gather, Show = true, MajorLanguage = ClientLanguage.Japanese, Color = "#DEBFF7" };
            MarketSold = new ChatChannel { Code = ChatCode.MarketSold, Show = true, MajorLanguage = ClientLanguage.Japanese, Color = "#CCCCCC" };
            GilReceive = new ChatChannel { Code = ChatCode.GilReceive, Show = true, MajorLanguage = ClientLanguage.Japanese, Color = "#CCCCCC" };
        }

        [ObservableProperty]
        [property: YamlIgnore]
        private ChatChannel _groupPartyField;

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
        [property: YamlMember(Alias = "emote")]
        private ChatChannel _emote;

        [ObservableProperty]
        [property: YamlMember(Alias = "emote_custom")]
        private ChatChannel _emoteCustom;

        [ObservableProperty]
        [property: YamlIgnore]
        private ChatChannel _groupLinkshell;

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
        [property: YamlIgnore]
        private ChatChannel _groupCwLinkshell;

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
        [property: YamlIgnore]
        private ChatChannel _groupCommunity;

        [ObservableProperty]
        [property: YamlMember(Alias = "freecompany")]
        private ChatChannel _freecompany;

        [ObservableProperty]
        [property: YamlMember(Alias = "novice")]
        private ChatChannel _novice;

        [ObservableProperty]
        [property: YamlIgnore]
        private ChatChannel _groupSystem;

        [ObservableProperty]
        [property: YamlMember(Alias = "system")]
        private ChatChannel _system;

        [ObservableProperty]
        [property: YamlMember(Alias = "notice")]
        private ChatChannel _notice;

        [ObservableProperty]
        [property: YamlMember(Alias = "error")]
        private ChatChannel _error;

        [ObservableProperty]
        [property: YamlMember(Alias = "gather")]
        private ChatChannel _gather;

        [ObservableProperty]
        [property: YamlMember(Alias = "gil_receive")]
        private ChatChannel _gilReceive;

        [ObservableProperty]
        [property: YamlMember(Alias = "market_sold")]
        private ChatChannel _marketSold;

        [ObservableProperty]
        [property: YamlMember(Alias = "recruitment")]
        private ChatChannel _recruitment;

        [ObservableProperty]
        [property: YamlMember(Alias = "npc_dialog")]
        private ChatChannel _npcDialog;

        [ObservableProperty]
        [property: YamlMember(Alias = "npc_announce")]
        private ChatChannel _npcAnnounce;

        [ObservableProperty]
        [property: YamlMember(Alias = "boss_quotes")]
        private ChatChannel _bossQuotes;
    }
#pragma warning restore CS8618
}
