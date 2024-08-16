using IronworksTranslator.Models.Settings;
using IronworksTranslator.Utils;
using IronworksTranslator.ViewModels.Pages;
using IronworksTranslator.ViewModels.Windows;
using IronworksTranslator.Views.Windows;
using System.Windows.Controls;
using Wpf.Ui.Controls;

namespace IronworksTranslator.Views.Pages
{
#pragma warning disable CS8602
    public partial class SettingsPage : INavigableView<SettingsViewModel>
    {
        public SettingsViewModel ViewModel { get; }
        private bool _isInitialized;

        public SettingsPage(SettingsViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = this;

            InitializeComponent();
            _isInitialized = true;
            ViewModel.CheckPartyFieldIntegrity();
            ViewModel.CheckCommunityIntegrity();
            ViewModel.CheckLinkShellIntegrity();
            ViewModel.CheckCwLinkShellIntegrity();
            viewModel.CheckSystemIntegrity();
        }

        private void ChatFontSize_ValueChanged(object sender, RoutedEventArgs e)
        {
            var numberBox = sender as NumberBox;
            numberBox.Value ??= IronworksSettings.Instance.ChatUiSettings.ChatboxFontSize;
            var chatWindowViewModel = App.GetService<ChatWindowViewModel>();
            chatWindowViewModel.ChangeChatFontSize((int)numberBox.Value);
        }

        private void ChatMargin_ValueChanged(object sender, RoutedEventArgs e)
        {
            var numberBox = sender as NumberBox;
            numberBox.Value ??= IronworksSettings.Instance.ChatUiSettings.ChatMargin;
            var chatWindowViewModel = App.GetService<ChatWindowViewModel>();
            chatWindowViewModel.ChangeChatMargin((int)numberBox.Value);
        }

        private void ChatFontFamily_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var comboBox = sender as ComboBox;
            if (comboBox.SelectedItem == null)
            {
                comboBox.SelectedItem = IronworksSettings.Instance.ChatUiSettings.Font;
            }
            var chatWindowViewModel = App.GetService<ChatWindowViewModel>();
            chatWindowViewModel.ChangeChatFontFamily((string)comboBox.SelectedValue);
        }

        private void GroupPartyFieldComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (!_isInitialized) return;
            var comboBox = sender as ComboBox;
            var index = comboBox.SelectedIndex;
            cbEcho.SelectedIndex = index;
            cbSay.SelectedIndex = index;
            cbYell.SelectedIndex = index;
            cbShout.SelectedIndex = index;
            cbTell.SelectedIndex = index;
            cbParty.SelectedIndex = index;
            cbAlliance.SelectedIndex = index;
            cbEmote.SelectedIndex = index;
            cbEmoteCustom.SelectedIndex = index;
        }

        private void GroupLinkShellComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitialized) return;
            var comboBox = sender as ComboBox;
            var index = comboBox.SelectedIndex;

            cbLinkshell1.SelectedIndex = index;
            cbLinkshell2.SelectedIndex = index;
            cbLinkshell3.SelectedIndex = index;
            cbLinkshell4.SelectedIndex = index;
            cbLinkshell5.SelectedIndex = index;
            cbLinkshell6.SelectedIndex = index;
            cbLinkshell7.SelectedIndex = index;
            cbLinkshell8.SelectedIndex = index;
        }

        private void GroupCommunityComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitialized) return;
            var comboBox = sender as ComboBox;
            var index = comboBox.SelectedIndex;

            cbFreecompany.SelectedIndex = index;
            cbNovice.SelectedIndex = index;
        }

        private void GroupCwLinkShellComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitialized) return;
            var comboBox = sender as ComboBox;
            var index = comboBox.SelectedIndex;

            cbCwLinkshell1.SelectedIndex = index;
            cbCwLinkshell2.SelectedIndex = index;
            cbCwLinkshell3.SelectedIndex = index;
            cbCwLinkshell4.SelectedIndex = index;
            cbCwLinkshell5.SelectedIndex = index;
            cbCwLinkshell6.SelectedIndex = index;
            cbCwLinkshell7.SelectedIndex = index;
            cbCwLinkshell8.SelectedIndex = index;
        }

        private void GroupSystemComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitialized) return;
            var comboBox = sender as ComboBox;
            var index = comboBox.SelectedIndex;

            cbGameNotice.SelectedIndex = index;
            cbGameSystem.SelectedIndex = index;
            cbGameError.SelectedIndex = index;
            cbNpcDialog.SelectedIndex = index;
            cbNpcAnnounce.SelectedIndex = index;
            cbBossQuotes.SelectedIndex = index;
            cbRecruitment.SelectedIndex = index;
            cbGather.SelectedIndex = index;
            cbMarketSold.SelectedIndex = index;
            cbGilReceive.SelectedIndex = index;
        }

        private void GroupPartyFieldOnButton_Click(object sender, RoutedEventArgs e)
        {
            tsShowEcho.IsChecked = true;
            tsShowSay.IsChecked = true;
            tsShowYell.IsChecked = true;
            tsShowShout.IsChecked = true;
            tsShowTell.IsChecked = true;
            tsShowParty.IsChecked = true;
            tsShowAlliance.IsChecked = true;
            tsShowEmote.IsChecked = true;
            tsShowEmoteCustom.IsChecked = true;
        }

        private void GroupPartyFieldOffButton_Click(object sender, RoutedEventArgs e)
        {
            tsShowEcho.IsChecked = false;
            tsShowSay.IsChecked = false;
            tsShowYell.IsChecked = false;
            tsShowShout.IsChecked = false;
            tsShowTell.IsChecked = false;
            tsShowParty.IsChecked = false;
            tsShowAlliance.IsChecked = false;
            tsShowEmote.IsChecked = false;
            tsShowEmoteCustom.IsChecked = false;
        }

        private void GroupCommunityOnButton_Click(object sender, RoutedEventArgs e)
        {
            tsShowNovice.IsChecked = true;
            tsShowFreecompany.IsChecked = true;
        }

        private void GroupCommunityOffButton_Click(object sender, RoutedEventArgs e)
        {
            tsShowNovice.IsChecked = false;
            tsShowFreecompany.IsChecked = false;
        }

        private void GroupLinkshellOnButton_Click(object sender, RoutedEventArgs e)
        {
            tsShowLinkshell1.IsChecked = true;
            tsShowLinkshell2.IsChecked = true;
            tsShowLinkshell3.IsChecked = true;
            tsShowLinkshell4.IsChecked = true;
            tsShowLinkshell5.IsChecked = true;
            tsShowLinkshell6.IsChecked = true;
            tsShowLinkshell7.IsChecked = true;
            tsShowLinkshell8.IsChecked = true;
        }

        private void GroupLinkshellOffButton_Click(object sender, RoutedEventArgs e)
        {
            tsShowLinkshell1.IsChecked = false;
            tsShowLinkshell2.IsChecked = false;
            tsShowLinkshell3.IsChecked = false;
            tsShowLinkshell4.IsChecked = false;
            tsShowLinkshell5.IsChecked = false;
            tsShowLinkshell6.IsChecked = false;
            tsShowLinkshell7.IsChecked = false;
            tsShowLinkshell8.IsChecked = false;
        }

        private void GroupCWLinkshellOnButton_Click(object sender, RoutedEventArgs e)
        {
            tsShowCwLinkshell1.IsChecked = true;
            tsShowCwLinkshell2.IsChecked = true;
            tsShowCwLinkshell3.IsChecked = true;
            tsShowCwLinkshell4.IsChecked = true;
            tsShowCwLinkshell5.IsChecked = true;
            tsShowCwLinkshell6.IsChecked = true;
            tsShowCwLinkshell7.IsChecked = true;
            tsShowCwLinkshell8.IsChecked = true;
        }

        private void GroupCWLinkshellOffButton_Click(object sender, RoutedEventArgs e)
        {
            tsShowCwLinkshell1.IsChecked = false;
            tsShowCwLinkshell2.IsChecked = false;
            tsShowCwLinkshell3.IsChecked = false;
            tsShowCwLinkshell4.IsChecked = false;
            tsShowCwLinkshell5.IsChecked = false;
            tsShowCwLinkshell6.IsChecked = false;
            tsShowCwLinkshell7.IsChecked = false;
            tsShowCwLinkshell8.IsChecked = false;
        }

        private void GroupSystemOnButton_Click(object sender, RoutedEventArgs e)
        {
            tsShowGameNotice.IsChecked = true;
            tsShowGameSystem.IsChecked = true;
            tsShowGameError.IsChecked = true;
            tsShowNpcDialog.IsChecked = true;
            tsShowNpcAnnounce.IsChecked = true;
            tsShowBossQuotes.IsChecked = true;
            tsShowRecruitment.IsChecked = true;
            tsShowGather.IsChecked = true;
            tsShowMarketSold.IsChecked = true;
            tsShowGilReceive.IsChecked = true;
        }

        private void GroupSystemOffButton_Click(object sender, RoutedEventArgs e)
        {
            tsShowGameNotice.IsChecked = false;
            tsShowGameSystem.IsChecked = false;
            tsShowGameError.IsChecked = false;
            tsShowNpcDialog.IsChecked = false;
            tsShowNpcAnnounce.IsChecked = false;
            tsShowBossQuotes.IsChecked = false;
            tsShowRecruitment.IsChecked = false;
            tsShowGather.IsChecked = false;
            tsShowMarketSold.IsChecked = false;
            tsShowGilReceive.IsChecked = false;
        }

        private void DeepLAPIListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var listView = sender as Wpf.Ui.Controls.ListView;
            ViewModel.SelectedDeeplApiKeyIndex = listView.SelectedIndex;
        }

        private void tbNewDeeplApiKey_TextChanged(object sender, TextChangedEventArgs e)
        {
            var tbName = sender as System.Windows.Controls.TextBox;
            if (tbName != null && tbName.Text != string.Empty)
            {
                ViewModel.NewDeepLApiKey = tbName.Text;
            }
        }

        private void cbTranslator_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var comboBox = sender as ComboBox;
            var selectedItem = ViewModel.TranslatorEngine;
            if (selectedItem == Models.Enums.TranslatorEngine.DeepL_API)
            {
                if (IronworksSettings.Instance.TranslatorSettings.DeeplApiKeys.Count == 0)
                {
                    System.Windows.MessageBox.Show(Localizer.GetString("settings.translator.engine.deepl_api.not_exists"));
                    comboBox.SelectedIndex = 0;
                    comboBox.SelectedItem = (Models.Enums.TranslatorEngine)0;
                }
            }
        }
    }
#pragma warning restore CS8602
}
