namespace IronworksTranslator.Settings
{
    public sealed class ChatSettings
    {
        public ChatSettings()
        {
            Echo = new Channel();
            Say = new Channel();
            Yell = new Channel();
            Shout = new Channel();
            Tell = new Channel();
            Party = new Channel();
            Alliance = new Channel();
            LinkShell1 = new Channel();
            LinkShell2 = new Channel();
            LinkShell3 = new Channel();
            LinkShell4 = new Channel();
            LinkShell5 = new Channel();
            LinkShell6 = new Channel();
            LinkShell7 = new Channel();
            LinkShell8 = new Channel();
            FreeCompany = new Channel();
            Novice = new Channel();
            System = new Channel();
            Error = new Channel();
            NPCDialog = new Channel();
            NPCAnnounce = new Channel();
            Recruitment = new Channel();
        }

        public Channel Echo;
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
        public Channel FreeCompany;
        public Channel Novice;
        public Channel System;
        public Channel Error;
        public Channel NPCDialog;
        public Channel NPCAnnounce;
        public Channel Recruitment;
        //public Channel // TODO: Crossworld Linkshell
    }
}
