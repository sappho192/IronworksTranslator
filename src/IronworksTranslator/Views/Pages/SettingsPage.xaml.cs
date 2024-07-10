using IronworksTranslator.Models.Settings;
using IronworksTranslator.ViewModels.Pages;
using Wpf.Ui.Controls;

namespace IronworksTranslator.Views.Pages
{
    public partial class SettingsPage : INavigableView<SettingsViewModel>
    {
        public SettingsViewModel ViewModel { get; }

        public SettingsPage(SettingsViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = this;

            InitializeComponent();
        }

        private void ChatFontSize_ValueChanged(object sender, RoutedEventArgs e)
        {
            var numberBox = sender as NumberBox;
            if (numberBox.Value == null)
            {
                numberBox.Value = IronworksSettings.Instance.chatUiSettings.ChatboxFontSize;
            }
        }
    }
}
