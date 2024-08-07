using IronworksTranslator.Views.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace IronworksTranslator.ViewModels.Pages
{
    public partial class DeveloperViewModel : ObservableObject
    {
        [ObservableProperty]
        private bool _isDraggable = true;
        [ObservableProperty]
        private Color _exampleColor = (Color)ColorConverter.ConvertFromString("#990293");

        [RelayCommand]
        private void OnAddChat()
        {
            var chatWindow = App.GetService<ChatWindow>();
            chatWindow.ViewModel.AddRandomMessage("Hello world!");
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

        partial void OnExampleColorChanged(Color value)
        {
            var a = 1;
        }
    }
}
