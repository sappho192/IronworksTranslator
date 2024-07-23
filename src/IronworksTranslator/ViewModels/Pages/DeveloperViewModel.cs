using IronworksTranslator.Views.Windows;
using System.Windows.Controls;

namespace IronworksTranslator.ViewModels.Pages
{
    public partial class DeveloperViewModel : ObservableObject
    {
        [ObservableProperty]
        private bool _isDraggable = true;

        [RelayCommand]
        private void OnAddChat()
        {
            var chatWindow = App.GetService<ChatWindow>();
            chatWindow.ViewModel.AddMessage("Hello world!");
            var scrollViewer = chatWindow.ChatPanel.Template.FindName("PART_ContentHost", chatWindow.ChatPanel) as ScrollViewer;
            scrollViewer.ScrollToBottom();
        }

        [RelayCommand]
        private void OnDietChat()
        {
            var chatWindow = App.GetService<ChatWindow>();
            chatWindow.ViewModel.Diet();
            var scrollViewer = chatWindow.ChatPanel.Template.FindName("PART_ContentHost", chatWindow.ChatPanel) as ScrollViewer;
            scrollViewer.ScrollToBottom();
        }
    }
}
