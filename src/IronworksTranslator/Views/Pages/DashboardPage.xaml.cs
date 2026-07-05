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

            Dispatcher.InvokeAsync(ViewModel.RunStartupPromptsAsync);
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
