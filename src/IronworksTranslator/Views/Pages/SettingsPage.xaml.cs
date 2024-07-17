using IronworksTranslator.Models.Settings;
using IronworksTranslator.ViewModels.Pages;
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
            if (numberBox.Value == null)
            {
                numberBox.Value = IronworksSettings.Instance.ChatUiSettings.ChatboxFontSize;
            }
        }

        private void GroupPartyFieldComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (!_isInitialized) return;
            var comboBox = sender as ComboBox;
            var index = comboBox.SelectedIndex;
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
    }
#pragma warning restore CS8602
}
