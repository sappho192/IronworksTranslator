using IronworksTranslator.ViewModels.Pages;
using IronworksTranslator.Views.Windows;
using Wpf.Ui.Controls;

namespace IronworksTranslator.Views.Pages
{
    /// <summary>
    /// DeveloperPage.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class DeveloperPage : INavigableView<DeveloperViewModel>
    {
        public DeveloperViewModel ViewModel { get; }

        public DeveloperPage(DeveloperViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = this;

            InitializeComponent();
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
