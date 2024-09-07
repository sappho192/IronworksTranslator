using IronworksTranslator.ViewModels.Pages;
using IronworksTranslator.Views.Windows;
using Wpf.Ui.Controls;

namespace IronworksTranslator.Views.Pages
{
    public partial class DashboardPage : INavigableView<DashboardViewModel>
    {
        public DashboardViewModel ViewModel { get; }

        public DashboardPage(DashboardViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = this;

            InitializeComponent();

            // Check translation model exists
            CheckModelIntegrity();
        }

        private void CheckModelIntegrity()
        {
            var window = new InitializationWindow();
            window.ShowDialog();
        }

        private void tsShowChatWindow_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleSwitch ui)
            {
                var chatWindow = App.GetService<ChatWindow>();
                if (ui.IsChecked == true)
                {
                    chatWindow.Show();
                }
                else
                {
                    chatWindow.Hide();
                }
            }
        }
    }
}
