using FontAwesome.WPF;
using IronworksTranslator.Core;
using Serilog;
using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Xceed.Wpf.Toolkit;

namespace IronworksTranslator
{
    /// <summary>
    /// MainWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MainWindow : Window
    {
        private IronworksContext ironworksContext;
        private IronworksSettings ironworksSettings;
        //private 
        private readonly Timer chatboxTimer;

        public MainWindow()
        {
            Topmost = true;
            InitializeComponent();

            var version = Assembly.GetExecutingAssembly().GetName().Version;
            mainWindow.Title += $" v{version}";
            Log.Information($"Current version: {version}");

            ironworksContext = IronworksContext.Instance();
            ironworksSettings = IronworksSettings.Instance;
            LoadSettings();

            const int period = 500;
            chatboxTimer = new Timer(RefreshChatbox, null, 0, period);
            Log.Debug($"New RefreshChatbox timer with period {period}ms");
        }

        private void LoadSettings()
        {
            Log.Debug("Applying settings from file");
            chatFontSizeSpinner.Value = ironworksSettings.UI.ChatTextboxFontSize;
            TranslatedChatBox.FontSize = ironworksSettings.UI.ChatTextboxFontSize;

            ClientLanguageComboBox.SelectedIndex = (int)ironworksSettings.Translator.NativeLanguage;

            TranslatorEngineComboBox.SelectedIndex = (int)ironworksSettings.Translator.DefaultTranslatorEngine;

            LoadChatSettings();

            Log.Debug("Settings applied");
        }

        private void LoadChatSettings()
        {
            Log.Debug("Applying chat settings");

            LoadChannelState(TellPanel, ironworksSettings.Chat.Tell);
            LoadChannelState(SayPanel, ironworksSettings.Chat.Say);
            LoadChannelState(YellPanel, ironworksSettings.Chat.Yell);
            LoadChannelState(ShoutPanel, ironworksSettings.Chat.Shout);
            LoadChannelState(PartyPanel, ironworksSettings.Chat.Party);
            LoadChannelState(AlliancePanel, ironworksSettings.Chat.Alliance);

            LoadChannelState(FreeCompanyPanel, ironworksSettings.Chat.FreeCompany);
            LoadChannelState(NovicePanel, ironworksSettings.Chat.Novice);
            LoadChannelState(LinkShell1Panel, ironworksSettings.Chat.LinkShell1);
            LoadChannelState(LinkShell2Panel, ironworksSettings.Chat.LinkShell2);
            LoadChannelState(LinkShell3Panel, ironworksSettings.Chat.LinkShell3);
            LoadChannelState(LinkShell4Panel, ironworksSettings.Chat.LinkShell4);
            LoadChannelState(LinkShell5Panel, ironworksSettings.Chat.LinkShell5);
            LoadChannelState(LinkShell6Panel, ironworksSettings.Chat.LinkShell6);
            LoadChannelState(LinkShell7Panel, ironworksSettings.Chat.LinkShell7);
            LoadChannelState(LinkShell8Panel, ironworksSettings.Chat.LinkShell8);
            LoadChannelState(CWLinkShell1Panel, ironworksSettings.Chat.CWLinkShell1);
            LoadChannelState(CWLinkShell2Panel, ironworksSettings.Chat.CWLinkShell2);
            LoadChannelState(CWLinkShell3Panel, ironworksSettings.Chat.CWLinkShell3);
            LoadChannelState(CWLinkShell4Panel, ironworksSettings.Chat.CWLinkShell4);
            LoadChannelState(CWLinkShell5Panel, ironworksSettings.Chat.CWLinkShell5);
            LoadChannelState(CWLinkShell6Panel, ironworksSettings.Chat.CWLinkShell6);
            LoadChannelState(CWLinkShell7Panel, ironworksSettings.Chat.CWLinkShell7);
            LoadChannelState(CWLinkShell8Panel, ironworksSettings.Chat.CWLinkShell8);

            Log.Debug("Chat settings applied");
        }

        private void LoadChannelState(StackPanel channelPanel, Settings.Channel channel)
        {
            var show = channel.Show;
            foreach (var ui in channelPanel.Children)
            {
                if (ui is Button)
                {
                    var icon = ((Button)ui).Content as ImageAwesome;
                    icon.Icon = show ? FontAwesomeIcon.Eye : FontAwesomeIcon.EyeSlash;
                }
                else if (ui is ComboBox)
                {
                    ((ComboBox)ui).SelectedIndex = (int)channel.MajorLanguage;
                }
            }
        }

        private void RefreshChatbox(object state)
        {
            UpdateChatbox();
        }

        private void Window_PreviewLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            Window window = (Window)sender;
            window.Topmost = true;
        }

        private void Window_PreviewTouchDown(object sender, TouchEventArgs e)
        {
            CaptureTouch(e.TouchDevice);
        }

        private void UpdateChatbox()
        {
            if (ChatQueue.q.Any())
            {// Should q be locked?
                var chat = ChatQueue.q.Take();
                int.TryParse(chat.Code, System.Globalization.NumberStyles.HexNumber, null, out var code);
                if (code <= 0x30)
                {
                    if (ironworksSettings.Chat.ChannelVisibility[(ChatCode)code])
                    {
                        Log.Debug("Chat: {@Chat}", chat);
                        var author = chat.Line.RemoveAfter(":");
                        var sentence = chat.Line.RemoveBefore(":");
                        var translated = ironworksContext.TranslateChat(sentence, ironworksSettings.Chat.ChannelLanguage[(ChatCode)code]);

                        Application.Current.Dispatcher.Invoke(() =>
                        {

                            TranslatedChatBox.Text +=
#if DEBUG
                        $"[{chat.Code}]{author}:{translated}{Environment.NewLine}";
#else
                        $"{author}:{translated}{Environment.NewLine}";
#endif
                        });
                    }
                }
                else
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        TranslatedChatBox.Text +=
#if DEBUG
                            $"[{chat.Code}]{chat.Line}{Environment.NewLine}";
#else
                            $"{chat.Line}{Environment.NewLine}";
#endif
                    });
                }
                Application.Current.Dispatcher.Invoke(() =>
                {
                    TranslatedChatBox.ScrollToEnd();
                    //TranslatedChatBox.ScrollToVerticalOffset(double.MaxValue);
                });
            }
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            if (System.Windows.MessageBox.Show("정말 종료할까요?", "프로그램 종료", MessageBoxButton.YesNo).Equals(MessageBoxResult.Yes))
            {
                Application.Current.Shutdown();
            }
        }

        private void CollapseButton_Click(object sender, RoutedEventArgs e)
        {
            var icon = (sender as Button).Content as ImageAwesome;
            icon.Icon =
                icon.Icon.Equals(FontAwesomeIcon.Bars) ?
                FontAwesomeIcon.AngleDoubleUp
                : FontAwesomeIcon.Bars;
            ToolbarGrid.Visibility =
                ToolbarGrid.Visibility.Equals(Visibility.Collapsed) ?
                 Visibility.Visible : Visibility.Collapsed;
            ShowOnly(UI.SettingsTab.None); // Hide all settings grid
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.MainWindow.WindowState = System.Windows.WindowState.Minimized;
        }

        private void GeneralSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleSettingsGrid(GeneralSettingsGrid, UI.SettingsTab.General);
        }

        private void LanguageSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleSettingsGrid(LanguageSettingsGrid, UI.SettingsTab.Language);
        }

        private void ChatSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleSettingsGrid(ChatSettingsGrid, UI.SettingsTab.Chat);
        }

        private void ToggleSettingsGrid(Grid settingsGrid, UI.SettingsTab setting)
        {
            if (settingsGrid.Visibility.Equals(Visibility.Hidden))
            {
                ShowOnly(setting);
            }
            else
            {
                settingsGrid.Visibility = Visibility.Hidden;
            }
        }

        private void ShowOnly(UI.SettingsTab active)
        {
            switch (active)
            {
                case UI.SettingsTab.General:
                    GeneralSettingsGrid.Visibility = Visibility.Visible;
                    LanguageSettingsGrid.Visibility = Visibility.Hidden;
                    ChatSettingsGrid.Visibility = Visibility.Hidden;
                    break;
                case UI.SettingsTab.Language:
                    GeneralSettingsGrid.Visibility = Visibility.Hidden;
                    LanguageSettingsGrid.Visibility = Visibility.Visible;
                    ChatSettingsGrid.Visibility = Visibility.Hidden;
                    break;
                case UI.SettingsTab.Chat:
                    GeneralSettingsGrid.Visibility = Visibility.Hidden;
                    LanguageSettingsGrid.Visibility = Visibility.Hidden;
                    ChatSettingsGrid.Visibility = Visibility.Visible;
                    break;
                case UI.SettingsTab.None:
                    GeneralSettingsGrid.Visibility = Visibility.Hidden;
                    LanguageSettingsGrid.Visibility = Visibility.Hidden;
                    ChatSettingsGrid.Visibility = Visibility.Hidden;
                    break;
            }
        }

        private void ChatFontSizeSpinner_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            IntegerUpDown spinner = sender as IntegerUpDown;
            if (ironworksSettings != null)
            {
                ironworksSettings.UI.ChatTextboxFontSize = spinner.Value ?? 6;
                TranslatedChatBox.FontSize = ironworksSettings.UI.ChatTextboxFontSize;
            }
        }

        private void ClientLanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ironworksSettings != null)
            {
                ComboBox box = sender as ComboBox;
                ironworksSettings.Translator.NativeLanguage = (ClientLanguage)box.SelectedIndex;
            }
        }

        private void TranslatorEngineComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ironworksSettings != null)
            {
                ComboBox box = sender as ComboBox;
                ironworksSettings.Translator.DefaultTranslatorEngine = (TranslatorEngine)box.SelectedIndex;
            }
        }

        private void ChangeMajorLanguage(Settings.Channel channel, ClientLanguage language)
        {
            if (ironworksSettings != null)
            {
                channel.MajorLanguage = language;
            }
        }

        private void FieldGroupComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ironworksSettings != null)
            {
                var languageIndex = ((ComboBox)sender).SelectedIndex;
                ChangeMajorLanguage(ironworksSettings.Chat.Tell, (ClientLanguage)languageIndex);
                ChangeMajorLanguage(ironworksSettings.Chat.Say, (ClientLanguage)languageIndex);
                ChangeMajorLanguage(ironworksSettings.Chat.Yell, (ClientLanguage)languageIndex);
                ChangeMajorLanguage(ironworksSettings.Chat.Shout, (ClientLanguage)languageIndex);
                ChangeMajorLanguage(ironworksSettings.Chat.Party, (ClientLanguage)languageIndex);
                ChangeMajorLanguage(ironworksSettings.Chat.Alliance, (ClientLanguage)languageIndex);

                TellComboBox.SelectedIndex = languageIndex;
                SayComboBox.SelectedIndex = languageIndex;
                YellComboBox.SelectedIndex = languageIndex;
                ShoutComboBox.SelectedIndex = languageIndex;
                PartyComboBox.SelectedIndex = languageIndex;
                AllianceComboBox.SelectedIndex = languageIndex;
            }
        }

        private void TellComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ironworksSettings != null)
            {
                ChangeMajorLanguage(ironworksSettings.Chat.Tell, (ClientLanguage)((ComboBox)sender).SelectedIndex);
            }
        }

        private void SayComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ironworksSettings != null)
            {
                ChangeMajorLanguage(ironworksSettings.Chat.Say, (ClientLanguage)((ComboBox)sender).SelectedIndex);
            }
        }

        private void YellComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ironworksSettings != null)
            {
                ChangeMajorLanguage(ironworksSettings.Chat.Yell, (ClientLanguage)((ComboBox)sender).SelectedIndex);
            }
        }

        private void ShoutComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ironworksSettings != null)
            {
                ChangeMajorLanguage(ironworksSettings.Chat.Shout, (ClientLanguage)((ComboBox)sender).SelectedIndex);
            }
        }

        private void PartyComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ironworksSettings != null)
            {
                ChangeMajorLanguage(ironworksSettings.Chat.Party, (ClientLanguage)((ComboBox)sender).SelectedIndex);
            }
        }

        private void AllianceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ironworksSettings != null)
            {
                ChangeMajorLanguage(ironworksSettings.Chat.Alliance, (ClientLanguage)((ComboBox)sender).SelectedIndex);
            }
        }

        private void DisplayFieldGroup(bool display, FontAwesomeIcon icon)
        {
            (TellShowButton.Content as ImageAwesome).Icon = icon;
            (SayShowButton.Content as ImageAwesome).Icon = icon;
            (YellShowButton.Content as ImageAwesome).Icon = icon;
            (ShoutShowButton.Content as ImageAwesome).Icon = icon;
            (PartyShowButton.Content as ImageAwesome).Icon = icon;
            (AllianceShowButton.Content as ImageAwesome).Icon = icon;

            ironworksSettings.Chat.Tell.Show = display;
            ironworksSettings.Chat.Say.Show = display;
            ironworksSettings.Chat.Yell.Show = display;
            ironworksSettings.Chat.Shout.Show = display;
            ironworksSettings.Chat.Party.Show = display;
            ironworksSettings.Chat.Alliance.Show = display;
        }

        private void FieldGroupShowButton_Click(object sender, RoutedEventArgs e)
        {
            DisplayFieldGroup(true, FontAwesomeIcon.Eye);
        }

        private void FieldGroupHideButton_Click(object sender, RoutedEventArgs e)
        {
            DisplayFieldGroup(false, FontAwesomeIcon.EyeSlash);
        }

        private void ToggleChannelShowButton(ImageAwesome icon, Settings.Channel channel)
        {
            if (icon.Icon.Equals(FontAwesomeIcon.Eye))
            {
                icon.Icon = FontAwesomeIcon.EyeSlash;
                channel.Show = false;
            }
            else
            {
                icon.Icon = FontAwesomeIcon.Eye;
                channel.Show = true;
            }
        }

        private void TellShowButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleChannelShowButton((sender as Button).Content as ImageAwesome, ironworksSettings.Chat.Tell);
        }

        private void SayShowButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleChannelShowButton((sender as Button).Content as ImageAwesome, ironworksSettings.Chat.Say);
        }

        private void YellShowButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleChannelShowButton((sender as Button).Content as ImageAwesome, ironworksSettings.Chat.Yell);
        }

        private void ShoutShowButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleChannelShowButton((sender as Button).Content as ImageAwesome, ironworksSettings.Chat.Shout);
        }

        private void PartyShowButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleChannelShowButton((sender as Button).Content as ImageAwesome, ironworksSettings.Chat.Party);
        }

        private void AllianceShowButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleChannelShowButton((sender as Button).Content as ImageAwesome, ironworksSettings.Chat.Alliance);
        }

        private void CommunityGroupHideButton_Click(object sender, RoutedEventArgs e)
        {
            DisplayCommunityGroup(false, FontAwesomeIcon.EyeSlash);
        }

        private void CommunityGroupShowButton_Click(object sender, RoutedEventArgs e)
        {
            DisplayCommunityGroup(true, FontAwesomeIcon.Eye);
        }

        private void DisplayCommunityGroup(bool display, FontAwesomeIcon icon)
        {
            (FreeCompanyShowButton.Content as ImageAwesome).Icon = icon;
            (NoviceShowButton.Content as ImageAwesome).Icon = icon;

            ironworksSettings.Chat.FreeCompany.Show = display;
            ironworksSettings.Chat.Novice.Show = display;
        }

        private void CommunityGroupComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ironworksSettings != null)
            {
                var languageIndex = ((ComboBox)sender).SelectedIndex;
                ChangeMajorLanguage(ironworksSettings.Chat.FreeCompany, (ClientLanguage)languageIndex);
                ChangeMajorLanguage(ironworksSettings.Chat.Novice, (ClientLanguage)languageIndex);

                FreeCompanyComboBox.SelectedIndex = languageIndex;
                NoviceComboBox.SelectedIndex = languageIndex;
            }
        }

        private void FreeCompanyComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ironworksSettings != null)
            {
                ChangeMajorLanguage(ironworksSettings.Chat.FreeCompany, (ClientLanguage)((ComboBox)sender).SelectedIndex);
            }
        }

        private void NoviceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ironworksSettings != null)
            {
                ChangeMajorLanguage(ironworksSettings.Chat.Novice, (ClientLanguage)((ComboBox)sender).SelectedIndex);
            }
        }

        private void FreeCompanyShowButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleChannelShowButton((sender as Button).Content as ImageAwesome, ironworksSettings.Chat.FreeCompany);
        }

        private void NoviceShowButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleChannelShowButton((sender as Button).Content as ImageAwesome, ironworksSettings.Chat.Novice);
        }

        private void LinkShellGroupShowButton_Click(object sender, RoutedEventArgs e)
        {
            DisplayLinkShellGroup(true, FontAwesomeIcon.Eye);
        }

        private void LinkShellGroupHideButton_Click(object sender, RoutedEventArgs e)
        {
            DisplayLinkShellGroup(false, FontAwesomeIcon.EyeSlash);
        }

        private void DisplayLinkShellGroup(bool display, FontAwesomeIcon icon)
        {
            (LinkShell1ShowButton.Content as ImageAwesome).Icon = icon;
            (LinkShell2ShowButton.Content as ImageAwesome).Icon = icon;
            (LinkShell3ShowButton.Content as ImageAwesome).Icon = icon;
            (LinkShell4ShowButton.Content as ImageAwesome).Icon = icon;
            (LinkShell5ShowButton.Content as ImageAwesome).Icon = icon;
            (LinkShell6ShowButton.Content as ImageAwesome).Icon = icon;
            (LinkShell7ShowButton.Content as ImageAwesome).Icon = icon;
            (LinkShell8ShowButton.Content as ImageAwesome).Icon = icon;

            ironworksSettings.Chat.LinkShell1.Show = display;
            ironworksSettings.Chat.LinkShell2.Show = display;
            ironworksSettings.Chat.LinkShell3.Show = display;
            ironworksSettings.Chat.LinkShell4.Show = display;
            ironworksSettings.Chat.LinkShell5.Show = display;
            ironworksSettings.Chat.LinkShell6.Show = display;
            ironworksSettings.Chat.LinkShell7.Show = display;
            ironworksSettings.Chat.LinkShell8.Show = display;
        }

        private void LinkShellGroupComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ironworksSettings != null)
            {
                var languageIndex = ((ComboBox)sender).SelectedIndex;
                ChangeMajorLanguage(ironworksSettings.Chat.LinkShell1, (ClientLanguage)languageIndex);
                ChangeMajorLanguage(ironworksSettings.Chat.LinkShell2, (ClientLanguage)languageIndex);
                ChangeMajorLanguage(ironworksSettings.Chat.LinkShell3, (ClientLanguage)languageIndex);
                ChangeMajorLanguage(ironworksSettings.Chat.LinkShell4, (ClientLanguage)languageIndex);
                ChangeMajorLanguage(ironworksSettings.Chat.LinkShell5, (ClientLanguage)languageIndex);
                ChangeMajorLanguage(ironworksSettings.Chat.LinkShell6, (ClientLanguage)languageIndex);
                ChangeMajorLanguage(ironworksSettings.Chat.LinkShell7, (ClientLanguage)languageIndex);
                ChangeMajorLanguage(ironworksSettings.Chat.LinkShell8, (ClientLanguage)languageIndex);

                LinkShell1ComboBox.SelectedIndex = languageIndex;
                LinkShell2ComboBox.SelectedIndex = languageIndex;
                LinkShell3ComboBox.SelectedIndex = languageIndex;
                LinkShell4ComboBox.SelectedIndex = languageIndex;
                LinkShell5ComboBox.SelectedIndex = languageIndex;
                LinkShell6ComboBox.SelectedIndex = languageIndex;
                LinkShell7ComboBox.SelectedIndex = languageIndex;
                LinkShell8ComboBox.SelectedIndex = languageIndex;
            }
        }

        private void LinkShell1ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ironworksSettings != null)
            {
                ChangeMajorLanguage(ironworksSettings.Chat.LinkShell1, (ClientLanguage)((ComboBox)sender).SelectedIndex);
            }
        }

        private void LinkShell2ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ironworksSettings != null)
            {
                ChangeMajorLanguage(ironworksSettings.Chat.LinkShell2, (ClientLanguage)((ComboBox)sender).SelectedIndex);
            }
        }

        private void LinkShell3ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ironworksSettings != null)
            {
                ChangeMajorLanguage(ironworksSettings.Chat.LinkShell3, (ClientLanguage)((ComboBox)sender).SelectedIndex);
            }
        }

        private void LinkShell4ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ironworksSettings != null)
            {
                ChangeMajorLanguage(ironworksSettings.Chat.LinkShell4, (ClientLanguage)((ComboBox)sender).SelectedIndex);
            }
        }

        private void LinkShell5ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ironworksSettings != null)
            {
                ChangeMajorLanguage(ironworksSettings.Chat.LinkShell5, (ClientLanguage)((ComboBox)sender).SelectedIndex);
            }
        }

        private void LinkShell6ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ironworksSettings != null)
            {
                ChangeMajorLanguage(ironworksSettings.Chat.LinkShell6, (ClientLanguage)((ComboBox)sender).SelectedIndex);
            }
        }

        private void LinkShell7ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ironworksSettings != null)
            {
                ChangeMajorLanguage(ironworksSettings.Chat.LinkShell7, (ClientLanguage)((ComboBox)sender).SelectedIndex);
            }
        }

        private void LinkShell8ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ironworksSettings != null)
            {
                ChangeMajorLanguage(ironworksSettings.Chat.LinkShell8, (ClientLanguage)((ComboBox)sender).SelectedIndex);
            }
        }

        private void LinkShell1ShowButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleChannelShowButton((sender as Button).Content as ImageAwesome, ironworksSettings.Chat.LinkShell1);
        }

        private void LinkShell2ShowButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleChannelShowButton((sender as Button).Content as ImageAwesome, ironworksSettings.Chat.LinkShell2);
        }


        private void LinkShell3ShowButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleChannelShowButton((sender as Button).Content as ImageAwesome, ironworksSettings.Chat.LinkShell3);
        }


        private void LinkShell4ShowButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleChannelShowButton((sender as Button).Content as ImageAwesome, ironworksSettings.Chat.LinkShell4);
        }


        private void LinkShell5ShowButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleChannelShowButton((sender as Button).Content as ImageAwesome, ironworksSettings.Chat.LinkShell5);
        }

        private void LinkShell6ShowButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleChannelShowButton((sender as Button).Content as ImageAwesome, ironworksSettings.Chat.LinkShell6);
        }

        private void LinkShell7ShowButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleChannelShowButton((sender as Button).Content as ImageAwesome, ironworksSettings.Chat.LinkShell7);
        }

        private void LinkShell8ShowButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleChannelShowButton((sender as Button).Content as ImageAwesome, ironworksSettings.Chat.LinkShell8);
        }

        private void CWLinkShellGroupShowButton_Click(object sender, RoutedEventArgs e)
        {
            DisplayCWLinkShellGroup(true, FontAwesomeIcon.Eye);
        }

        private void CWLinkShellGroupHideButton_Click(object sender, RoutedEventArgs e)
        {
            DisplayCWLinkShellGroup(false, FontAwesomeIcon.EyeSlash);
        }

        private void DisplayCWLinkShellGroup(bool display, FontAwesomeIcon icon)
        {
            (CWLinkShell1ShowButton.Content as ImageAwesome).Icon = icon;
            (CWLinkShell2ShowButton.Content as ImageAwesome).Icon = icon;
            (CWLinkShell3ShowButton.Content as ImageAwesome).Icon = icon;
            (CWLinkShell4ShowButton.Content as ImageAwesome).Icon = icon;
            (CWLinkShell5ShowButton.Content as ImageAwesome).Icon = icon;
            (CWLinkShell6ShowButton.Content as ImageAwesome).Icon = icon;
            (CWLinkShell7ShowButton.Content as ImageAwesome).Icon = icon;
            (CWLinkShell8ShowButton.Content as ImageAwesome).Icon = icon;

            ironworksSettings.Chat.CWLinkShell1.Show = display;
            ironworksSettings.Chat.CWLinkShell2.Show = display;
            ironworksSettings.Chat.CWLinkShell3.Show = display;
            ironworksSettings.Chat.CWLinkShell4.Show = display;
            ironworksSettings.Chat.CWLinkShell5.Show = display;
            ironworksSettings.Chat.CWLinkShell6.Show = display;
            ironworksSettings.Chat.CWLinkShell7.Show = display;
            ironworksSettings.Chat.CWLinkShell8.Show = display;
        }

        private void CWLinkShellGroupComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ironworksSettings != null)
            {
                var languageIndex = ((ComboBox)sender).SelectedIndex;
                ChangeMajorLanguage(ironworksSettings.Chat.CWLinkShell1, (ClientLanguage)languageIndex);
                ChangeMajorLanguage(ironworksSettings.Chat.CWLinkShell2, (ClientLanguage)languageIndex);
                ChangeMajorLanguage(ironworksSettings.Chat.CWLinkShell3, (ClientLanguage)languageIndex);
                ChangeMajorLanguage(ironworksSettings.Chat.CWLinkShell4, (ClientLanguage)languageIndex);
                ChangeMajorLanguage(ironworksSettings.Chat.CWLinkShell5, (ClientLanguage)languageIndex);
                ChangeMajorLanguage(ironworksSettings.Chat.CWLinkShell6, (ClientLanguage)languageIndex);
                ChangeMajorLanguage(ironworksSettings.Chat.CWLinkShell7, (ClientLanguage)languageIndex);
                ChangeMajorLanguage(ironworksSettings.Chat.CWLinkShell8, (ClientLanguage)languageIndex);

                CWLinkShell1ComboBox.SelectedIndex = languageIndex;
                CWLinkShell2ComboBox.SelectedIndex = languageIndex;
                CWLinkShell3ComboBox.SelectedIndex = languageIndex;
                CWLinkShell4ComboBox.SelectedIndex = languageIndex;
                CWLinkShell5ComboBox.SelectedIndex = languageIndex;
                CWLinkShell6ComboBox.SelectedIndex = languageIndex;
                CWLinkShell7ComboBox.SelectedIndex = languageIndex;
                CWLinkShell8ComboBox.SelectedIndex = languageIndex;
            }
        }

        private void CWLinkShell1ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ironworksSettings != null)
            {
                ChangeMajorLanguage(ironworksSettings.Chat.CWLinkShell1, (ClientLanguage)((ComboBox)sender).SelectedIndex);
            }
        }

        private void CWLinkShell2ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ironworksSettings != null)
            {
                ChangeMajorLanguage(ironworksSettings.Chat.CWLinkShell2, (ClientLanguage)((ComboBox)sender).SelectedIndex);
            }
        }

        private void CWLinkShell3ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ironworksSettings != null)
            {
                ChangeMajorLanguage(ironworksSettings.Chat.CWLinkShell3, (ClientLanguage)((ComboBox)sender).SelectedIndex);
            }
        }

        private void CWLinkShell4ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ironworksSettings != null)
            {
                ChangeMajorLanguage(ironworksSettings.Chat.CWLinkShell4, (ClientLanguage)((ComboBox)sender).SelectedIndex);
            }
        }

        private void CWLinkShell5ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ironworksSettings != null)
            {
                ChangeMajorLanguage(ironworksSettings.Chat.CWLinkShell5, (ClientLanguage)((ComboBox)sender).SelectedIndex);
            }
        }

        private void CWLinkShell6ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ironworksSettings != null)
            {
                ChangeMajorLanguage(ironworksSettings.Chat.CWLinkShell6, (ClientLanguage)((ComboBox)sender).SelectedIndex);
            }
        }

        private void CWLinkShell7ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ironworksSettings != null)
            {
                ChangeMajorLanguage(ironworksSettings.Chat.CWLinkShell7, (ClientLanguage)((ComboBox)sender).SelectedIndex);
            }
        }

        private void CWLinkShell8ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ironworksSettings != null)
            {
                ChangeMajorLanguage(ironworksSettings.Chat.CWLinkShell8, (ClientLanguage)((ComboBox)sender).SelectedIndex);
            }
        }

        private void CWLinkShell1ShowButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleChannelShowButton((sender as Button).Content as ImageAwesome, ironworksSettings.Chat.CWLinkShell1);
        }

        private void CWLinkShell2ShowButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleChannelShowButton((sender as Button).Content as ImageAwesome, ironworksSettings.Chat.CWLinkShell2);
        }

        private void CWLinkShell3ShowButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleChannelShowButton((sender as Button).Content as ImageAwesome, ironworksSettings.Chat.CWLinkShell3);
        }

        private void CWLinkShell4ShowButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleChannelShowButton((sender as Button).Content as ImageAwesome, ironworksSettings.Chat.CWLinkShell4);
        }

        private void CWLinkShell5ShowButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleChannelShowButton((sender as Button).Content as ImageAwesome, ironworksSettings.Chat.CWLinkShell5);
        }

        private void CWLinkShell6ShowButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleChannelShowButton((sender as Button).Content as ImageAwesome, ironworksSettings.Chat.CWLinkShell6);
        }

        private void CWLinkShell7ShowButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleChannelShowButton((sender as Button).Content as ImageAwesome, ironworksSettings.Chat.CWLinkShell7);
        }

        private void CWLinkShell8ShowButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleChannelShowButton((sender as Button).Content as ImageAwesome, ironworksSettings.Chat.CWLinkShell8);
        }
    }
}
