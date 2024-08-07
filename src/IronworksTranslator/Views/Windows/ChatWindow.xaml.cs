using IronworksTranslator.ViewModels.Windows;

namespace IronworksTranslator.Views.Windows
{
    /// <summary>
    /// ChatWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class ChatWindow : Window
    {
        public ChatWindowViewModel ViewModel { get; }

        public ChatWindow(ChatWindowViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = this;

            InitializeComponent();
            
            ChatPanel.Document = ViewModel.ChatDocument;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            Hide();
        }

        protected override void OnContentRendered(EventArgs e)
        {
            base.OnContentRendered(e);

            ViewModel.AddRandomMessage("Lorem ipsum dolor sit amet, consectetur adipiscing elit.");
        }
    }
}
