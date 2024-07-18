using IronworksTranslator.Models;
using IronworksTranslator.Models.Enums;
using IronworksTranslator.Models.Settings;
using System.Windows.Controls;
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
        private ClientLanguage _groupPartyFieldLanguage;// = IronworksSettings.Instance.ChannelSettings.GroupPartyField.MajorLanguage;
        [ObservableProperty]
        private int _groupPartyFieldLanguageIndex = -1;

        [ObservableProperty]
        private Visibility _groupPartyFieldHintVisibility = Visibility.Hidden;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private ClientLanguage _sayLanguage = IronworksSettings.Instance.ChannelSettings.Say.MajorLanguage;
        [ObservableProperty]
        private int _sayLanguageIndex = (int)IronworksSettings.Instance.ChannelSettings.Say.MajorLanguage;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private ClientLanguage _yellLanguage = IronworksSettings.Instance.ChannelSettings.Yell.MajorLanguage;
        [ObservableProperty]
        private int _yellLanguageIndex = (int)IronworksSettings.Instance.ChannelSettings.Yell.MajorLanguage;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private ClientLanguage _shoutLanguage = IronworksSettings.Instance.ChannelSettings.Shout.MajorLanguage;
        [ObservableProperty]
        private int _shoutLanguageIndex = (int)IronworksSettings.Instance.ChannelSettings.Shout.MajorLanguage;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private ClientLanguage _tellLanguage = IronworksSettings.Instance.ChannelSettings.Tell.MajorLanguage;
        [ObservableProperty]
        private int _tellLanguageIndex = (int)IronworksSettings.Instance.ChannelSettings.Tell.MajorLanguage;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private ClientLanguage _partyLanguage = IronworksSettings.Instance.ChannelSettings.Party.MajorLanguage;
        [ObservableProperty]
        private int _partyLanguageIndex = (int)IronworksSettings.Instance.ChannelSettings.Party.MajorLanguage;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private ClientLanguage _allianceLanguage = IronworksSettings.Instance.ChannelSettings.Alliance.MajorLanguage;
        [ObservableProperty]
        private int _allianceLanguageIndex = (int)IronworksSettings.Instance.ChannelSettings.Alliance.MajorLanguage;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private ClientLanguage _emoteLanguage = IronworksSettings.Instance.ChannelSettings.Emote.MajorLanguage;
        [ObservableProperty]
        private int _emoteLanguageIndex = (int)IronworksSettings.Instance.ChannelSettings.Emote.MajorLanguage;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private ClientLanguage _emoteCustomLanguage = IronworksSettings.Instance.ChannelSettings.EmoteCustom.MajorLanguage;
        [ObservableProperty]
        private int _emoteCustomLanguageIndex = (int)IronworksSettings.Instance.ChannelSettings.EmoteCustom.MajorLanguage;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private bool _showSayChannel = IronworksSettings.Instance.ChannelSettings.Say.Show;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private bool _showYellChannel = IronworksSettings.Instance.ChannelSettings.Yell.Show;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private bool _showShoutChannel = IronworksSettings.Instance.ChannelSettings.Shout.Show;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private bool _showTellChannel = IronworksSettings.Instance.ChannelSettings.Tell.Show;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private bool _showPartyChannel = IronworksSettings.Instance.ChannelSettings.Party.Show;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private bool _showAllianceChannel = IronworksSettings.Instance.ChannelSettings.Alliance.Show;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private bool _showEmoteChannel = IronworksSettings.Instance.ChannelSettings.Emote.Show;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private bool _showEmoteCustomChannel = IronworksSettings.Instance.ChannelSettings.EmoteCustom.Show;

        #region Commands
        [RelayCommand]
        private void OnShowSayChannelChanged(SymbolIcon icon)
        {
            if (ShowSayChannel)
            {
                icon.Symbol = SymbolRegular.EyeOff24;
                ShowSayChannel = false;
            }
            else
            {
                icon.Symbol = SymbolRegular.Eye24;
                ShowSayChannel = true;
            }
        }

        [RelayCommand]
        private void OnShowYellChannelChanged(SymbolIcon icon)
        {
            if (ShowYellChannel)
            {
                icon.Symbol = SymbolRegular.EyeOff24;
                ShowYellChannel = false;
            }
            else
            {
                icon.Symbol = SymbolRegular.Eye24;
                ShowYellChannel = true;
            }
        }

        [RelayCommand]
        private void OnShowShoutChannelChanged(SymbolIcon icon)
        {
            if (ShowShoutChannel)
            {
                icon.Symbol = SymbolRegular.EyeOff24;
                ShowShoutChannel = false;
            }
            else
            {
                icon.Symbol = SymbolRegular.Eye24;
                ShowShoutChannel = true;
            }
        }

        [RelayCommand]
        private void OnShowTellChannelChanged(SymbolIcon icon)
        {
            if (ShowTellChannel)
            {
                icon.Symbol = SymbolRegular.EyeOff24;
                ShowTellChannel = false;
            }
            else
            {
                icon.Symbol = SymbolRegular.Eye24;
                ShowTellChannel = true;
            }
        }

        [RelayCommand]
        private void OnShowPartyChannelChanged(SymbolIcon icon)
        {
            if (ShowPartyChannel)
            {
                icon.Symbol = SymbolRegular.EyeOff24;
                ShowPartyChannel = false;
            }
            else
            {
                icon.Symbol = SymbolRegular.Eye24;
                ShowPartyChannel = true;
            }
        }

        [RelayCommand]
        private void OnShowAllianceChannelChanged(SymbolIcon icon)
        {
            if (ShowAllianceChannel)
            {
                icon.Symbol = SymbolRegular.EyeOff24;
                ShowAllianceChannel = false;
            }
            else
            {
                icon.Symbol = SymbolRegular.Eye24;
                ShowAllianceChannel = true;
            }
        }

        [RelayCommand]
        private void OnShowEmoteChannelChanged(SymbolIcon icon)
        {
            if (ShowEmoteChannel)
            {
                icon.Symbol = SymbolRegular.EyeOff24;
                ShowEmoteChannel = false;
            }
            else
            {
                icon.Symbol = SymbolRegular.Eye24;
                ShowEmoteChannel = true;
            }
        }

        [RelayCommand]
        private void OnShowEmoteCustomChannelChanged(SymbolIcon icon)
        {
            if (ShowEmoteCustomChannel)
            {
                icon.Symbol = SymbolRegular.EyeOff24;
                ShowEmoteCustomChannel = false;
            }
            else
            {
                icon.Symbol = SymbolRegular.Eye24;
                ShowEmoteCustomChannel = true;
            }
        }
        #endregion
        #region Listeners
        partial void OnGroupPartyFieldLanguageChanged(ClientLanguage value)
        {
            CheckPartyFieldIntegrity();
        }
        partial void OnSayLanguageChanged(ClientLanguage value)
        {
            CheckPartyFieldIntegrity();
        }
        partial void OnYellLanguageChanged(ClientLanguage value)
        {
            CheckPartyFieldIntegrity();
        }
        partial void OnShoutLanguageChanged(ClientLanguage value)
        {
            CheckPartyFieldIntegrity();
        }
        partial void OnTellLanguageChanged(ClientLanguage value)
        {
            CheckPartyFieldIntegrity();
        }
        partial void OnPartyLanguageChanged(ClientLanguage value)
        {
            CheckPartyFieldIntegrity();
        }
        partial void OnAllianceLanguageChanged(ClientLanguage value)
        {
            CheckPartyFieldIntegrity();
        }
        partial void OnEmoteLanguageChanged(ClientLanguage value)
        {
            CheckPartyFieldIntegrity();
        }
        partial void OnEmoteCustomLanguageChanged(ClientLanguage value)
        {
            CheckPartyFieldIntegrity();
        }
        public void CheckPartyFieldIntegrity()
        {
            if (SayLanguage == YellLanguage &&
                SayLanguage == ShoutLanguage &&
                SayLanguage == TellLanguage &&
                SayLanguage == PartyLanguage &&
                SayLanguage == AllianceLanguage &&
                SayLanguage == EmoteLanguage &&
                SayLanguage == EmoteCustomLanguage)
            {
                GroupPartyFieldHintVisibility = Visibility.Hidden;
                //GroupPartyFieldLanguageIndex = SayLanguageIndex;
            }
            else
            {
                GroupPartyFieldHintVisibility = Visibility.Visible;
            }
        }
        #endregion
        #endregion

        #region Linkshells
        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private ClientLanguage _groupLinkShellLanguage;
        [ObservableProperty]
        private int _groupLinkShellLanguageIndex = -1;

        [ObservableProperty]
        private Visibility _groupLinkShellHintVisibility = Visibility.Hidden;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private ClientLanguage _linkshell1Language = IronworksSettings.Instance.ChannelSettings.Linkshell1.MajorLanguage;
        [ObservableProperty]
        private int _linkshell1LanguageIndex = (int)IronworksSettings.Instance.ChannelSettings.Linkshell1.MajorLanguage;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private ClientLanguage _linkshell2Language = IronworksSettings.Instance.ChannelSettings.Linkshell2.MajorLanguage;
        [ObservableProperty]
        private int _linkshell2LanguageIndex = (int)IronworksSettings.Instance.ChannelSettings.Linkshell2.MajorLanguage;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private ClientLanguage _linkshell3Language = IronworksSettings.Instance.ChannelSettings.Linkshell3.MajorLanguage;
        [ObservableProperty]
        private int _linkshell3LanguageIndex = (int)IronworksSettings.Instance.ChannelSettings.Linkshell3.MajorLanguage;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private ClientLanguage _linkshell4Language = IronworksSettings.Instance.ChannelSettings.Linkshell4.MajorLanguage;
        [ObservableProperty]
        private int _linkshell4LanguageIndex = (int)IronworksSettings.Instance.ChannelSettings.Linkshell4.MajorLanguage;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private ClientLanguage _linkshell5Language = IronworksSettings.Instance.ChannelSettings.Linkshell5.MajorLanguage;
        [ObservableProperty]
        private int _linkshell5LanguageIndex = (int)IronworksSettings.Instance.ChannelSettings.Linkshell5.MajorLanguage;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private ClientLanguage _linkshell6Language = IronworksSettings.Instance.ChannelSettings.Linkshell6.MajorLanguage;
        [ObservableProperty]
        private int _linkshell6LanguageIndex = (int)IronworksSettings.Instance.ChannelSettings.Linkshell6.MajorLanguage;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private ClientLanguage _linkshell7Language = IronworksSettings.Instance.ChannelSettings.Linkshell7.MajorLanguage;
        [ObservableProperty]
        private int _linkshell7LanguageIndex = (int)IronworksSettings.Instance.ChannelSettings.Linkshell7.MajorLanguage;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private ClientLanguage _linkshell8Language = IronworksSettings.Instance.ChannelSettings.Linkshell8.MajorLanguage;
        [ObservableProperty]
        private int _linkshell8LanguageIndex = (int)IronworksSettings.Instance.ChannelSettings.Linkshell8.MajorLanguage;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private bool _showLinkshell1Channel = IronworksSettings.Instance.ChannelSettings.Linkshell1.Show;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private bool _showLinkshell2Channel = IronworksSettings.Instance.ChannelSettings.Linkshell2.Show;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private bool _showLinkshell3Channel = IronworksSettings.Instance.ChannelSettings.Linkshell3.Show;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private bool _showLinkshell4Channel = IronworksSettings.Instance.ChannelSettings.Linkshell4.Show;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private bool _showLinkshell5Channel = IronworksSettings.Instance.ChannelSettings.Linkshell5.Show;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private bool _showLinkshell6Channel = IronworksSettings.Instance.ChannelSettings.Linkshell6.Show;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private bool _showLinkshell7Channel = IronworksSettings.Instance.ChannelSettings.Linkshell7.Show;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private bool _showLinkshell8Channel = IronworksSettings.Instance.ChannelSettings.Linkshell8.Show;

        #region Commands
        [RelayCommand]
        private void OnShowLinkshell1ChannelChanged(SymbolIcon icon)
        {
            if (ShowLinkshell1Channel)
            {
                icon.Symbol = SymbolRegular.EyeOff24;
                ShowLinkshell1Channel = false;
            }
            else
            {
                icon.Symbol = SymbolRegular.Eye24;
                ShowLinkshell1Channel = true;
            }
        }

        [RelayCommand]
        private void OnShowLinkshell2ChannelChanged(SymbolIcon icon)
        {
            if (ShowLinkshell2Channel)
            {
                icon.Symbol = SymbolRegular.EyeOff24;
                ShowLinkshell2Channel = false;
            }
            else
            {
                icon.Symbol = SymbolRegular.Eye24;
                ShowLinkshell2Channel = true;
            }
        }

        [RelayCommand]
        private void OnShowLinkshell3ChannelChanged(SymbolIcon icon)
        {
            if (ShowLinkshell3Channel)
            {
                icon.Symbol = SymbolRegular.EyeOff24;
                ShowLinkshell3Channel = false;
            }
            else
            {
                icon.Symbol = SymbolRegular.Eye24;
                ShowLinkshell3Channel = true;
            }
        }

        [RelayCommand]
        private void OnShowLinkshell4ChannelChanged(SymbolIcon icon)
        {
            if (ShowLinkshell4Channel)
            {
                icon.Symbol = SymbolRegular.EyeOff24;
                ShowLinkshell4Channel = false;
            }
            else
            {
                icon.Symbol = SymbolRegular.Eye24;
                ShowLinkshell4Channel = true;
            }
        }

        [RelayCommand]
        private void OnShowLinkshell5ChannelChanged(SymbolIcon icon)
        {
            if (ShowLinkshell5Channel)
            {
                icon.Symbol = SymbolRegular.EyeOff24;
                ShowLinkshell5Channel = false;
            }
            else
            {
                icon.Symbol = SymbolRegular.Eye24;
                ShowLinkshell5Channel = true;
            }
        }

        [RelayCommand]
        private void OnShowLinkshell6ChannelChanged(SymbolIcon icon)
        {
            if (ShowLinkshell6Channel)
            {
                icon.Symbol = SymbolRegular.EyeOff24;
                ShowLinkshell6Channel = false;
            }
            else
            {
                icon.Symbol = SymbolRegular.Eye24;
                ShowLinkshell6Channel = true;
            }
        }

        [RelayCommand]
        private void OnShowLinkshell7ChannelChanged(SymbolIcon icon)
        {
            if (ShowLinkshell7Channel)
            {
                icon.Symbol = SymbolRegular.EyeOff24;
                ShowLinkshell7Channel = false;
            }
            else
            {
                icon.Symbol = SymbolRegular.Eye24;
                ShowLinkshell7Channel = true;
            }
        }

        [RelayCommand]
        private void OnShowLinkshell8ChannelChanged(SymbolIcon icon)
        {
            if (ShowLinkshell8Channel)
            {
                icon.Symbol = SymbolRegular.EyeOff24;
                ShowLinkshell8Channel = false;
            }
            else
            {
                icon.Symbol = SymbolRegular.Eye24;
                ShowLinkshell8Channel = true;
            }
        }
        #endregion
        #region Listeners
        partial void OnGroupLinkShellLanguageChanged(ClientLanguage value)
        {
            CheckLinkShellIntegrity();
        }
        partial void OnLinkshell1LanguageChanged(ClientLanguage value)
        {
            CheckLinkShellIntegrity();
        }
        partial void OnLinkshell2LanguageChanged(ClientLanguage value)
        {
            CheckLinkShellIntegrity();
        }
        partial void OnLinkshell3LanguageChanged(ClientLanguage value)
        {
            CheckLinkShellIntegrity();
        }
        partial void OnLinkshell4LanguageChanged(ClientLanguage value)
        {
            CheckLinkShellIntegrity();
        }
        partial void OnLinkshell5LanguageChanged(ClientLanguage value)
        {
            CheckLinkShellIntegrity();
        }
        partial void OnLinkshell6LanguageChanged(ClientLanguage value)
        {
            CheckLinkShellIntegrity();
        }
        partial void OnLinkshell7LanguageChanged(ClientLanguage value)
        {
            CheckLinkShellIntegrity();
        }
        partial void OnLinkshell8LanguageChanged(ClientLanguage value)
        {
            CheckLinkShellIntegrity();
        }
        public void CheckLinkShellIntegrity()
        {
            if (Linkshell1Language == Linkshell2Language &&
                Linkshell1Language == Linkshell3Language &&
                Linkshell1Language == Linkshell4Language &&
                Linkshell1Language == Linkshell5Language &&
                Linkshell1Language == Linkshell6Language &&
                Linkshell1Language == Linkshell7Language &&
                Linkshell1Language == Linkshell8Language )
            {
                GroupLinkShellHintVisibility = Visibility.Hidden;
            }
            else
            {
                GroupLinkShellHintVisibility = Visibility.Visible;
            }
        }
        #endregion
        #endregion

        #region CrossWorld Linkshells
        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private ClientLanguage _groupCwLinkShellLanguage;
        [ObservableProperty]
        private int _groupCwLinkShellLanguageIndex = -1;

        [ObservableProperty]
        private Visibility _groupCwLinkShellHintVisibility = Visibility.Hidden;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private ClientLanguage _cwLinkshell1Language = IronworksSettings.Instance.ChannelSettings.CwLinkshell1.MajorLanguage;
        [ObservableProperty]
        private int _cwLinkshell1LanguageIndex = (int)IronworksSettings.Instance.ChannelSettings.CwLinkshell1.MajorLanguage;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private ClientLanguage _cwLinkshell2Language = IronworksSettings.Instance.ChannelSettings.CwLinkshell2.MajorLanguage;
        [ObservableProperty]
        private int _cwLinkshell2LanguageIndex = (int)IronworksSettings.Instance.ChannelSettings.CwLinkshell2.MajorLanguage;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private ClientLanguage _cwLinkshell3Language = IronworksSettings.Instance.ChannelSettings.CwLinkshell3.MajorLanguage;
        [ObservableProperty]
        private int _cwLinkshell3LanguageIndex = (int)IronworksSettings.Instance.ChannelSettings.CwLinkshell3.MajorLanguage;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private ClientLanguage _cwLinkshell4Language = IronworksSettings.Instance.ChannelSettings.CwLinkshell4.MajorLanguage;
        [ObservableProperty]
        private int _cwLinkshell4LanguageIndex = (int)IronworksSettings.Instance.ChannelSettings.CwLinkshell4.MajorLanguage;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private ClientLanguage _cwLinkshell5Language = IronworksSettings.Instance.ChannelSettings.CwLinkshell5.MajorLanguage;
        [ObservableProperty]
        private int _cwLinkshell5LanguageIndex = (int)IronworksSettings.Instance.ChannelSettings.CwLinkshell5.MajorLanguage;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private ClientLanguage _cwLinkshell6Language = IronworksSettings.Instance.ChannelSettings.CwLinkshell6.MajorLanguage;
        [ObservableProperty]
        private int _cwLinkshell6LanguageIndex = (int)IronworksSettings.Instance.ChannelSettings.CwLinkshell6.MajorLanguage;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private ClientLanguage _cwLinkshell7Language = IronworksSettings.Instance.ChannelSettings.CwLinkshell7.MajorLanguage;
        [ObservableProperty]
        private int _cwLinkshell7LanguageIndex = (int)IronworksSettings.Instance.ChannelSettings.CwLinkshell7.MajorLanguage;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private ClientLanguage _cwLinkshell8Language = IronworksSettings.Instance.ChannelSettings.CwLinkshell8.MajorLanguage;
        [ObservableProperty]
        private int _cwLinkshell8LanguageIndex = (int)IronworksSettings.Instance.ChannelSettings.CwLinkshell8.MajorLanguage;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private bool _showCwLinkshell1Channel = IronworksSettings.Instance.ChannelSettings.Linkshell1.Show;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private bool _showCwLinkshell2Channel = IronworksSettings.Instance.ChannelSettings.Linkshell2.Show;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private bool _showCwLinkshell3Channel = IronworksSettings.Instance.ChannelSettings.Linkshell3.Show;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private bool _showCwLinkshell4Channel = IronworksSettings.Instance.ChannelSettings.Linkshell4.Show;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private bool _showCwLinkshell5Channel = IronworksSettings.Instance.ChannelSettings.Linkshell5.Show;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private bool _showCwLinkshell6Channel = IronworksSettings.Instance.ChannelSettings.Linkshell6.Show;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private bool _showCwLinkshell7Channel = IronworksSettings.Instance.ChannelSettings.Linkshell7.Show;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private bool _showCwLinkshell8Channel = IronworksSettings.Instance.ChannelSettings.Linkshell8.Show;

        #region Commands
        [RelayCommand]
        private void OnShowCwLinkshell1ChannelChanged(SymbolIcon icon)
        {
            if (ShowCwLinkshell1Channel)
            {
                icon.Symbol = SymbolRegular.EyeOff24;
                ShowCwLinkshell1Channel = false;
            }
            else
            {
                icon.Symbol = SymbolRegular.Eye24;
                ShowCwLinkshell1Channel = true;
            }
        }

        [RelayCommand]
        private void OnShowCwLinkshell2ChannelChanged(SymbolIcon icon)
        {
            if (ShowCwLinkshell2Channel)
            {
                icon.Symbol = SymbolRegular.EyeOff24;
                ShowCwLinkshell2Channel = false;
            }
            else
            {
                icon.Symbol = SymbolRegular.Eye24;
                ShowCwLinkshell2Channel = true;
            }
        }

        [RelayCommand]
        private void OnShowCwLinkshell3ChannelChanged(SymbolIcon icon)
        {
            if (ShowCwLinkshell3Channel)
            {
                icon.Symbol = SymbolRegular.EyeOff24;
                ShowCwLinkshell3Channel = false;
            }
            else
            {
                icon.Symbol = SymbolRegular.Eye24;
                ShowCwLinkshell3Channel = true;
            }
        }

        [RelayCommand]
        private void OnShowCwLinkshell4ChannelChanged(SymbolIcon icon)
        {
            if (ShowCwLinkshell4Channel)
            {
                icon.Symbol = SymbolRegular.EyeOff24;
                ShowCwLinkshell4Channel = false;
            }
            else
            {
                icon.Symbol = SymbolRegular.Eye24;
                ShowCwLinkshell4Channel = true;
            }
        }

        [RelayCommand]
        private void OnShowCwLinkshell5ChannelChanged(SymbolIcon icon)
        {
            if (ShowCwLinkshell5Channel)
            {
                icon.Symbol = SymbolRegular.EyeOff24;
                ShowCwLinkshell5Channel = false;
            }
            else
            {
                icon.Symbol = SymbolRegular.Eye24;
                ShowCwLinkshell5Channel = true;
            }
        }

        [RelayCommand]
        private void OnShowCwLinkshell6ChannelChanged(SymbolIcon icon)
        {
            if (ShowCwLinkshell6Channel)
            {
                icon.Symbol = SymbolRegular.EyeOff24;
                ShowCwLinkshell6Channel = false;
            }
            else
            {
                icon.Symbol = SymbolRegular.Eye24;
                ShowCwLinkshell6Channel = true;
            }
        }

        [RelayCommand]
        private void OnShowCwLinkshell7ChannelChanged(SymbolIcon icon)
        {
            if (ShowCwLinkshell7Channel)
            {
                icon.Symbol = SymbolRegular.EyeOff24;
                ShowCwLinkshell7Channel = false;
            }
            else
            {
                icon.Symbol = SymbolRegular.Eye24;
                ShowCwLinkshell7Channel = true;
            }
        }

        [RelayCommand]
        private void OnShowCwLinkshell8ChannelChanged(SymbolIcon icon)
        {
            if (ShowCwLinkshell8Channel)
            {
                icon.Symbol = SymbolRegular.EyeOff24;
                ShowCwLinkshell8Channel = false;
            }
            else
            {
                icon.Symbol = SymbolRegular.Eye24;
                ShowCwLinkshell8Channel = true;
            }
        }
        #endregion
        #region Listeners
        partial void OnGroupCwLinkShellLanguageChanged(ClientLanguage value)
        {
            CheckCwLinkShellIntegrity();
        }

        partial void OnCwLinkshell1LanguageChanged(ClientLanguage value)
        {
            CheckCwLinkShellIntegrity();
        }

        partial void OnCwLinkshell2LanguageChanged(ClientLanguage value)
        {
            CheckCwLinkShellIntegrity();
        }

        partial void OnCwLinkshell3LanguageChanged(ClientLanguage value)
        {
            CheckCwLinkShellIntegrity();
        }

        partial void OnCwLinkshell4LanguageChanged(ClientLanguage value)
        {
            CheckCwLinkShellIntegrity();
        }

        partial void OnCwLinkshell5LanguageChanged(ClientLanguage value)
        {
            CheckCwLinkShellIntegrity();
        }

        partial void OnCwLinkshell6LanguageChanged(ClientLanguage value)
        {
            CheckCwLinkShellIntegrity();
        }

        partial void OnCwLinkshell7LanguageChanged(ClientLanguage value)
        {
            CheckCwLinkShellIntegrity();
        }

        partial void OnCwLinkshell8LanguageChanged(ClientLanguage value)
        {
            CheckCwLinkShellIntegrity();
        }

        public void CheckCwLinkShellIntegrity()
        {
            if (CwLinkshell1Language == CwLinkshell2Language &&
                CwLinkshell1Language == CwLinkshell3Language &&
                CwLinkshell1Language == CwLinkshell4Language &&
                CwLinkshell1Language == CwLinkshell5Language &&
                CwLinkshell1Language == CwLinkshell6Language &&
                CwLinkshell1Language == CwLinkshell7Language &&
                CwLinkshell1Language == CwLinkshell8Language)
            {
                GroupCwLinkShellHintVisibility = Visibility.Hidden;
            }
            else
            {
                GroupCwLinkShellHintVisibility = Visibility.Visible;
            }
        }
        #endregion
        #endregion

        #region Community
        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private ClientLanguage _groupCommunityLanguage;
        [ObservableProperty]
        private int _groupCommunityLanguageIndex = -1;

        [ObservableProperty]
        private Visibility _groupCommunityHintVisibility = Visibility.Hidden;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private ClientLanguage _freecompanyLanguage = IronworksSettings.Instance.ChannelSettings.Freecompany.MajorLanguage;
        [ObservableProperty]
        private int _freecompanyLanguageIndex = (int)IronworksSettings.Instance.ChannelSettings.Freecompany.MajorLanguage;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private ClientLanguage _noviceLanguage = IronworksSettings.Instance.ChannelSettings.Novice.MajorLanguage;
        [ObservableProperty]
        private int _noviceLanguageIndex = (int)IronworksSettings.Instance.ChannelSettings.Novice.MajorLanguage;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private bool _showFreecompanyChannel = IronworksSettings.Instance.ChannelSettings.Freecompany.Show;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private bool _showNoviceChannel = IronworksSettings.Instance.ChannelSettings.Novice.Show;
        #region Commands
        [RelayCommand]
        private void OnShowFreecompanyChannelChanged(SymbolIcon icon)
        {
            if (ShowFreecompanyChannel)
            {
                icon.Symbol = SymbolRegular.EyeOff24;
                ShowFreecompanyChannel = false;
            }
            else
            {
                icon.Symbol = SymbolRegular.Eye24;
                ShowFreecompanyChannel = true;
            }
        }

        [RelayCommand]
        private void OnShowNoviceChannelChanged(SymbolIcon icon)
        {
            if (ShowNoviceChannel)
            {
                icon.Symbol = SymbolRegular.EyeOff24;
                ShowNoviceChannel = false;
            }
            else
            {
                icon.Symbol = SymbolRegular.Eye24;
                ShowNoviceChannel = true;
            }
        }
        #endregion
        #region Listeners
        partial void OnGroupCommunityLanguageChanged(ClientLanguage value)
        {
            CheckCommunityIntegrity();
        }

        partial void OnNoviceLanguageChanged(ClientLanguage value)
        {
            CheckCommunityIntegrity();
        }

        partial void OnFreecompanyLanguageChanged(ClientLanguage value)
        {
            CheckCommunityIntegrity();
        }

        public void CheckCommunityIntegrity()
        {
            if (NoviceLanguage == FreecompanyLanguage)
            {
                GroupCommunityHintVisibility = Visibility.Hidden;
            }
            else
            {
                GroupCommunityHintVisibility = Visibility.Visible;
            }
        }
        #endregion
        #endregion

        #region System
        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private ClientLanguage _groupSystemLanguage;
        [ObservableProperty]
        private int _groupSystemLanguageIndex = -1;

        [ObservableProperty]
        private Visibility _groupSystemHintVisibility = Visibility.Hidden;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private ClientLanguage _gameSystemLanguage = IronworksSettings.Instance.ChannelSettings.System.MajorLanguage;
        [ObservableProperty]
        private int _gameSystemLanguageIndex = (int)IronworksSettings.Instance.ChannelSettings.System.MajorLanguage;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private ClientLanguage _gameNoticeLanguage = IronworksSettings.Instance.ChannelSettings.Notice.MajorLanguage;
        [ObservableProperty]
        private int _gameNoticeLanguageIndex = (int)IronworksSettings.Instance.ChannelSettings.Notice.MajorLanguage;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private ClientLanguage _gameErrorLanguage = IronworksSettings.Instance.ChannelSettings.Error.MajorLanguage;
        [ObservableProperty]
        private int _gameErrorLanguageIndex = (int)IronworksSettings.Instance.ChannelSettings.Error.MajorLanguage;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private ClientLanguage _gatherLanguage = IronworksSettings.Instance.ChannelSettings.Gather.MajorLanguage;
        [ObservableProperty]
        private int _gatherLanguageIndex = (int)IronworksSettings.Instance.ChannelSettings.Gather.MajorLanguage;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private ClientLanguage _gilReceiveLanguage = IronworksSettings.Instance.ChannelSettings.GilReceive.MajorLanguage;
        [ObservableProperty]
        private int _gilReceiveLanguageIndex = (int)IronworksSettings.Instance.ChannelSettings.GilReceive.MajorLanguage;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private ClientLanguage _npcDialogLanguage = IronworksSettings.Instance.ChannelSettings.NpcDialog.MajorLanguage;
        [ObservableProperty]
        private int _npcDialogLanguageIndex = (int)IronworksSettings.Instance.ChannelSettings.NpcDialog.MajorLanguage;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private ClientLanguage _npcAnnounceLanguage = IronworksSettings.Instance.ChannelSettings.NpcAnnounce.MajorLanguage;
        [ObservableProperty]
        private int _npcAnnounceLanguageIndex = (int)IronworksSettings.Instance.ChannelSettings.NpcAnnounce.MajorLanguage;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private ClientLanguage _marketSoldLanguage = IronworksSettings.Instance.ChannelSettings.MarketSold.MajorLanguage;
        [ObservableProperty]
        private int _marketSoldLanguageIndex = (int)IronworksSettings.Instance.ChannelSettings.MarketSold.MajorLanguage;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private ClientLanguage _recruitmentLanguage = IronworksSettings.Instance.ChannelSettings.Recruitment.MajorLanguage;
        [ObservableProperty]
        private int _recruitmentLanguageIndex = (int)IronworksSettings.Instance.ChannelSettings.Recruitment.MajorLanguage;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private ClientLanguage _bossQuotesLanguage = IronworksSettings.Instance.ChannelSettings.BossQuotes.MajorLanguage;
        [ObservableProperty]
        private int _bossQuotesLanguageIndex = (int)IronworksSettings.Instance.ChannelSettings.BossQuotes.MajorLanguage;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private bool _showGameSystemChannel = IronworksSettings.Instance.ChannelSettings.System.Show;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private bool _showGameNoticeChannel = IronworksSettings.Instance.ChannelSettings.Notice.Show;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private bool _showGameErrorChannel = IronworksSettings.Instance.ChannelSettings.Error.Show;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private bool _showGatherChannel = IronworksSettings.Instance.ChannelSettings.Gather.Show;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private bool _showGilReceiveChannel = IronworksSettings.Instance.ChannelSettings.GilReceive.Show;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private bool _showNpcDialogChannel = IronworksSettings.Instance.ChannelSettings.NpcDialog.Show;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private bool _showNpcAnnounceChannel = IronworksSettings.Instance.ChannelSettings.NpcAnnounce.Show;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private bool _showMarketSoldChannel = IronworksSettings.Instance.ChannelSettings.MarketSold.Show;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private bool _showRecruitmentChannel = IronworksSettings.Instance.ChannelSettings.Recruitment.Show;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private bool _showBossQuotesChannel = IronworksSettings.Instance.ChannelSettings.BossQuotes.Show;
        #region Commands
        [RelayCommand]
        private void OnShowGameSystemChannelChanged(SymbolIcon icon)
        {
            if (ShowGameSystemChannel)
            {
                icon.Symbol = SymbolRegular.EyeOff24;
                ShowGameSystemChannel = false;
            }
            else
            {
                icon.Symbol = SymbolRegular.Eye24;
                ShowGameSystemChannel = true;
            }
        }

        [RelayCommand]
        private void OnShowGameNoticeChannelChanged(SymbolIcon icon)
        {
            if (ShowGameNoticeChannel)
            {
                icon.Symbol = SymbolRegular.EyeOff24;
                ShowGameNoticeChannel = false;
            }
            else
            {
                icon.Symbol = SymbolRegular.Eye24;
                ShowGameNoticeChannel = true;
            }
        }

        [RelayCommand]
        private void OnShowGameErrorChannelChanged(SymbolIcon icon)
        {
            if (ShowGameErrorChannel)
            {
                icon.Symbol = SymbolRegular.EyeOff24;
                ShowGameErrorChannel = false;
            }
            else
            {
                icon.Symbol = SymbolRegular.Eye24;
                ShowGameErrorChannel = true;
            }
        }

        [RelayCommand]
        private void OnShowGatherChannelChanged(SymbolIcon icon)
        {
            if (ShowGatherChannel)
            {
                icon.Symbol = SymbolRegular.EyeOff24;
                ShowGatherChannel = false;
            }
            else
            {
                icon.Symbol = SymbolRegular.Eye24;
                ShowGatherChannel = true;
            }
        }

        [RelayCommand]
        private void OnShowGilReceiveChannelChanged(SymbolIcon icon)
        {
            if (ShowGilReceiveChannel)
            {
                icon.Symbol = SymbolRegular.EyeOff24;
                ShowGilReceiveChannel = false;
            }
            else
            {
                icon.Symbol = SymbolRegular.Eye24;
                ShowGilReceiveChannel = true;
            }
        }

        [RelayCommand]
        private void OnShowNpcDialogChannelChanged(SymbolIcon icon)
        {
            if (ShowNpcDialogChannel)
            {
                icon.Symbol = SymbolRegular.EyeOff24;
                ShowNpcDialogChannel = false;
            }
            else
            {
                icon.Symbol = SymbolRegular.Eye24;
                ShowNpcDialogChannel = true;
            }
        }

        [RelayCommand]
        private void OnShowNpcAnnounceChannelChanged(SymbolIcon icon)
        {
            if (ShowNpcAnnounceChannel)
            {
                icon.Symbol = SymbolRegular.EyeOff24;
                ShowNpcAnnounceChannel = false;
            }
            else
            {
                icon.Symbol = SymbolRegular.Eye24;
                ShowNpcAnnounceChannel = true;
            }
        }

        [RelayCommand]
        private void OnShowMarketSoldChannelChanged(SymbolIcon icon)
        {
            if (ShowMarketSoldChannel)
            {
                icon.Symbol = SymbolRegular.EyeOff24;
                ShowMarketSoldChannel = false;
            }
            else
            {
                icon.Symbol = SymbolRegular.Eye24;
                ShowMarketSoldChannel = true;
            }
        }

        [RelayCommand]
        private void OnShowRecruitmentChannelChanged(SymbolIcon icon)
        {
            if (ShowRecruitmentChannel)
            {
                icon.Symbol = SymbolRegular.EyeOff24;
                ShowRecruitmentChannel = false;
            }
            else
            {
                icon.Symbol = SymbolRegular.Eye24;
                ShowRecruitmentChannel = true;
            }
        }

        [RelayCommand]
        private void OnShowBossQuotesChannelChanged(SymbolIcon icon)
        {
            if (ShowBossQuotesChannel)
            {
                icon.Symbol = SymbolRegular.EyeOff24;
                ShowBossQuotesChannel = false;
            }
            else
            {
                icon.Symbol = SymbolRegular.Eye24;
                ShowBossQuotesChannel = true;
            }
        }
        #endregion
        #region Listeners
        partial void OnGroupSystemLanguageChanged(ClientLanguage value)
        {
            CheckSystemIntegrity();
        }

        partial void OnGameSystemLanguageChanged(ClientLanguage value)
        {
            CheckSystemIntegrity();
        }

        partial void OnGameNoticeLanguageChanged(ClientLanguage value)
        {
            CheckSystemIntegrity();
        }

        partial void OnGameErrorLanguageChanged(ClientLanguage value)
        {
            CheckSystemIntegrity();
        }

        partial void OnGilReceiveLanguageChanged(ClientLanguage value)
        {
            CheckSystemIntegrity();
        }

        partial void OnNpcDialogLanguageChanged(ClientLanguage value)
        {
            CheckSystemIntegrity();
        }

        partial void OnNpcAnnounceLanguageChanged(ClientLanguage value)
        {
            CheckSystemIntegrity();
        }

        partial void OnMarketSoldLanguageChanged(ClientLanguage value)
        {
            CheckSystemIntegrity();
        }

        partial void OnRecruitmentLanguageChanged(ClientLanguage value)
        {
            CheckSystemIntegrity();
        }

        partial void OnBossQuotesLanguageChanged(ClientLanguage value)
        {
            CheckSystemIntegrity();
        }

        partial void OnGatherLanguageChanged(ClientLanguage value)
        {
            CheckSystemIntegrity();
        }


        public void CheckSystemIntegrity()
        {
            if (GameNoticeLanguage == GameSystemLanguage &&
                GameNoticeLanguage == GameErrorLanguage &&
                GameNoticeLanguage == NpcDialogLanguage &&
                GameNoticeLanguage == NpcAnnounceLanguage &&
                GameNoticeLanguage == BossQuotesLanguage &&
                GameNoticeLanguage == RecruitmentLanguage &&
                GameNoticeLanguage == GatherLanguage &&
                GameNoticeLanguage == MarketSoldLanguage &&
                GameNoticeLanguage == GilReceiveLanguage)
            {
                GroupSystemHintVisibility = Visibility.Hidden;
            }
            else
            {
                GroupSystemHintVisibility = Visibility.Visible;
            }
        }
        #endregion
        #endregion
    }
#pragma warning restore CS8602
}
