using IronworksTranslator.Core;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace IronworksTranslator.Settings
{
    public sealed class ChatSettings
    {
        public ChatSettings()
        {
            Echo = new Channel(ChatCode.Echo);
            Emote = new Channel(ChatCode.Emote);
            Say = new Channel(ChatCode.Say);
            Yell = new Channel(ChatCode.Yell);
            Shout = new Channel(ChatCode.Shout);
            Tell = new Channel(ChatCode.Tell);
            Party = new Channel(ChatCode.Party);
            Alliance = new Channel(ChatCode.Alliance);
            LinkShell1 = new Channel(ChatCode.LinkShell1);
            LinkShell2 = new Channel(ChatCode.LinkShell2);
            LinkShell3 = new Channel(ChatCode.LinkShell3);
            LinkShell4 = new Channel(ChatCode.LinkShell4);
            LinkShell5 = new Channel(ChatCode.LinkShell5);
            LinkShell6 = new Channel(ChatCode.LinkShell6);
            LinkShell7 = new Channel(ChatCode.LinkShell7);
            LinkShell8 = new Channel(ChatCode.LinkShell8);
            CWLinkShell1 = new Channel(ChatCode.CWLinkShell1);
            CWLinkShell2 = new Channel(ChatCode.CWLinkShell2);
            CWLinkShell3 = new Channel(ChatCode.CWLinkShell3);
            CWLinkShell4 = new Channel(ChatCode.CWLinkShell4);
            CWLinkShell5 = new Channel(ChatCode.CWLinkShell5);
            CWLinkShell6 = new Channel(ChatCode.CWLinkShell6);
            CWLinkShell7 = new Channel(ChatCode.CWLinkShell7);
            CWLinkShell8 = new Channel(ChatCode.CWLinkShell8);
            FreeCompany = new Channel(ChatCode.FreeCompany);
            Novice = new Channel(ChatCode.Novice);
            System = new Channel(ChatCode.System);
            Notice = new Channel(ChatCode.Notice);
            Error = new Channel(ChatCode.Error);
            NPCDialog = new Channel(ChatCode.NPCDialog);
            NPCAnnounce = new Channel(ChatCode.NPCAnnounce);
            MarketSold = new Channel(ChatCode.MarketSold);
            Recruitment = new Channel(ChatCode.Recruitment);

            ChannelVisibility = new Dictionary<ChatCode, bool>() {
                {ChatCode.Echo, Echo.Show },
                {ChatCode.Emote, Echo.Show },
                {ChatCode.Say, Say.Show },
                {ChatCode.Yell, Yell.Show },
                {ChatCode.Shout, Shout.Show },
                {ChatCode.Tell, Tell.Show },
                {ChatCode.Party, Party.Show },
                {ChatCode.Alliance, Alliance.Show },
                {ChatCode.LinkShell1, LinkShell1.Show },
                {ChatCode.LinkShell2, LinkShell2.Show },
                {ChatCode.LinkShell3, LinkShell3.Show },
                {ChatCode.LinkShell4, LinkShell4.Show },
                {ChatCode.LinkShell5, LinkShell5.Show },
                {ChatCode.LinkShell6, LinkShell6.Show },
                {ChatCode.LinkShell7, LinkShell7.Show },
                {ChatCode.LinkShell8, LinkShell8.Show },
                {ChatCode.CWLinkShell1, CWLinkShell1.Show },
                {ChatCode.CWLinkShell2, CWLinkShell2.Show },
                {ChatCode.CWLinkShell3, CWLinkShell3.Show },
                {ChatCode.CWLinkShell4, CWLinkShell4.Show },
                {ChatCode.CWLinkShell5, CWLinkShell5.Show },
                {ChatCode.CWLinkShell6, CWLinkShell6.Show },
                {ChatCode.CWLinkShell7, CWLinkShell7.Show },
                {ChatCode.CWLinkShell8, CWLinkShell8.Show },
                {ChatCode.FreeCompany, FreeCompany.Show },
                {ChatCode.Novice, Novice.Show },
                {ChatCode.System, System.Show },
                {ChatCode.Notice, Notice.Show },
                {ChatCode.Error, Error.Show },
                {ChatCode.NPCDialog, NPCDialog.Show },
                {ChatCode.NPCAnnounce, NPCAnnounce.Show },
                {ChatCode.MarketSold, MarketSold.Show },
                {ChatCode.Recruitment, Recruitment.Show },
            };

            ChannelLanguage = new Dictionary<ChatCode, ClientLanguage>(){
                {ChatCode.Echo, Echo.MajorLanguage },
                {ChatCode.Emote, Emote.MajorLanguage },
                {ChatCode.Say, Say.MajorLanguage },
                {ChatCode.Yell, Yell.MajorLanguage },
                {ChatCode.Shout, Shout.MajorLanguage },
                {ChatCode.Tell, Tell.MajorLanguage },
                {ChatCode.Party, Party.MajorLanguage },
                {ChatCode.Alliance, Alliance.MajorLanguage },
                {ChatCode.LinkShell1, LinkShell1.MajorLanguage },
                {ChatCode.LinkShell2, LinkShell2.MajorLanguage },
                {ChatCode.LinkShell3, LinkShell3.MajorLanguage },
                {ChatCode.LinkShell4, LinkShell4.MajorLanguage },
                {ChatCode.LinkShell5, LinkShell5.MajorLanguage },
                {ChatCode.LinkShell6, LinkShell6.MajorLanguage },
                {ChatCode.LinkShell7, LinkShell7.MajorLanguage },
                {ChatCode.LinkShell8, LinkShell8.MajorLanguage },
                {ChatCode.CWLinkShell1, CWLinkShell1.MajorLanguage },
                {ChatCode.CWLinkShell2, CWLinkShell2.MajorLanguage },
                {ChatCode.CWLinkShell3, CWLinkShell3.MajorLanguage },
                {ChatCode.CWLinkShell4, CWLinkShell4.MajorLanguage },
                {ChatCode.CWLinkShell5, CWLinkShell5.MajorLanguage },
                {ChatCode.CWLinkShell6, CWLinkShell6.MajorLanguage },
                {ChatCode.CWLinkShell7, CWLinkShell7.MajorLanguage },
                {ChatCode.CWLinkShell8, CWLinkShell8.MajorLanguage },
                {ChatCode.FreeCompany, FreeCompany.MajorLanguage },
                {ChatCode.Novice, Novice.MajorLanguage },
                {ChatCode.System, System.MajorLanguage },
                {ChatCode.Notice, Notice.MajorLanguage },
                {ChatCode.Error, Error.MajorLanguage },
                {ChatCode.NPCDialog, NPCDialog.MajorLanguage },
                {ChatCode.NPCAnnounce, NPCAnnounce.MajorLanguage },
                {ChatCode.MarketSold, MarketSold.MajorLanguage },
                {ChatCode.Recruitment, Recruitment.MajorLanguage },
            };

            Echo.OnSettingsChanged += Channel_OnSettingsChanged;
            Emote.OnSettingsChanged += Channel_OnSettingsChanged;
            Say.OnSettingsChanged += Channel_OnSettingsChanged;
            Yell.OnSettingsChanged += Channel_OnSettingsChanged;
            Shout.OnSettingsChanged += Channel_OnSettingsChanged;
            Tell.OnSettingsChanged += Channel_OnSettingsChanged;
            Party.OnSettingsChanged += Channel_OnSettingsChanged;
            Alliance.OnSettingsChanged += Channel_OnSettingsChanged;
            LinkShell1.OnSettingsChanged += Channel_OnSettingsChanged;
            LinkShell2.OnSettingsChanged += Channel_OnSettingsChanged;
            LinkShell3.OnSettingsChanged += Channel_OnSettingsChanged;
            LinkShell4.OnSettingsChanged += Channel_OnSettingsChanged;
            LinkShell5.OnSettingsChanged += Channel_OnSettingsChanged;
            LinkShell6.OnSettingsChanged += Channel_OnSettingsChanged;
            LinkShell7.OnSettingsChanged += Channel_OnSettingsChanged;
            LinkShell8.OnSettingsChanged += Channel_OnSettingsChanged;
            CWLinkShell1.OnSettingsChanged += Channel_OnSettingsChanged;
            CWLinkShell2.OnSettingsChanged += Channel_OnSettingsChanged;
            CWLinkShell3.OnSettingsChanged += Channel_OnSettingsChanged;
            CWLinkShell4.OnSettingsChanged += Channel_OnSettingsChanged;
            CWLinkShell5.OnSettingsChanged += Channel_OnSettingsChanged;
            CWLinkShell6.OnSettingsChanged += Channel_OnSettingsChanged;
            CWLinkShell7.OnSettingsChanged += Channel_OnSettingsChanged;
            CWLinkShell8.OnSettingsChanged += Channel_OnSettingsChanged;
            FreeCompany.OnSettingsChanged += Channel_OnSettingsChanged;
            Novice.OnSettingsChanged += Channel_OnSettingsChanged;
            System.OnSettingsChanged += Channel_OnSettingsChanged;
            Notice.OnSettingsChanged += Channel_OnSettingsChanged;
            Error.OnSettingsChanged += Channel_OnSettingsChanged;
            NPCDialog.OnSettingsChanged += Channel_OnSettingsChanged;
            NPCAnnounce.OnSettingsChanged += Channel_OnSettingsChanged;
            MarketSold.OnSettingsChanged += Channel_OnSettingsChanged;
            Recruitment.OnSettingsChanged += Channel_OnSettingsChanged;
        }

        private void Channel_OnSettingsChanged(object sender, string name, object value)
        {
            var channel = sender as Channel;
            switch (name)
            {
                case "Show":
                    ChannelVisibility[channel.Code] = (bool)value;
                    break;
                case "MajorLanguage":
                    ChannelLanguage[channel.Code] = (ClientLanguage)value;
                    break;
                default:
                    break;
            }
        }

        [JsonIgnore]
        public Dictionary<ChatCode, bool> ChannelVisibility;
        [JsonIgnore]
        public Dictionary<ChatCode, ClientLanguage> ChannelLanguage;

        public Channel Echo;
        public Channel Emote;
        public Channel Say;
        public Channel Yell;
        public Channel Shout;
        public Channel Tell;
        public Channel Party;
        public Channel Alliance;
        public Channel LinkShell1;
        public Channel LinkShell2;
        public Channel LinkShell3;
        public Channel LinkShell4;
        public Channel LinkShell5;
        public Channel LinkShell6;
        public Channel LinkShell7;
        public Channel LinkShell8;
        public Channel CWLinkShell1;
        public Channel CWLinkShell2;
        public Channel CWLinkShell3;
        public Channel CWLinkShell4;
        public Channel CWLinkShell5;
        public Channel CWLinkShell6;
        public Channel CWLinkShell7;
        public Channel CWLinkShell8;
        public Channel FreeCompany;
        public Channel Novice;
        public Channel System;
        public Channel Notice;
        public Channel Error;
        public Channel NPCDialog;
        public Channel NPCAnnounce;
        public Channel MarketSold;
        public Channel Recruitment;
    }
}
