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
        }

        private void ShowChatWindowButton_Click(object sender, RoutedEventArgs e)
        {
            var chatWindow = App.GetService<ChatWindow>();
            if (chatWindow.Visibility != Visibility.Visible)
            {
                chatWindow.Show();
            }
        }

        private void HideChatWindowButton_Click(object sender, RoutedEventArgs e)
        {
            var chatWindow = App.GetService<ChatWindow>();
            if (chatWindow.Visibility == Visibility.Visible)
            {
                chatWindow.Hide();
            }
        }

        private void DisableChatWindowGripButton_Click(object sender, RoutedEventArgs e)
        {
            var chatWindow = App.GetService<ChatWindow>();
            chatWindow.ResizeMode = ResizeMode.NoResize;
        }

        private void EnableChatWindowGripButton_Click(object sender, RoutedEventArgs e)
        {
            var chatWindow = App.GetService<ChatWindow>();
            chatWindow.ResizeMode = ResizeMode.CanResizeWithGrip;
        }

        private void EnableChatWindowDragButton_Click(object sender, RoutedEventArgs e)
        {
            var chatWindow = App.GetService<ChatWindow>();
            chatWindow.ViewModel.IsDraggable = true;
        }

        private void DisableChatWindowDragButton_Click(object sender, RoutedEventArgs e)
        {
            var chatWindow = App.GetService<ChatWindow>();
            chatWindow.ViewModel.IsDraggable = false;
        }
    }
}
