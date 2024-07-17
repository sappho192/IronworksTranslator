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
    }
#pragma warning restore CS8602
}
