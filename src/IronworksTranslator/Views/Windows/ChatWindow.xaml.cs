using IronworksTranslator.Models.Settings;
using IronworksTranslator.ViewModels.Windows;
using System.Windows.Threading;

namespace IronworksTranslator.Views.Windows
{
    /// <summary>
    /// ChatWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class ChatWindow : Window
    {
        public ChatWindowViewModel ViewModel { get; }
        private readonly DispatcherTimer _resizeEndTimer = new ();

#pragma warning disable CS8602
        public ChatWindow(ChatWindowViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = this;
            Topmost = true;

            InitializeComponent();
            if (IronworksSettings.Instance.ChatUiSettings.IsResizable)
            {
                ResizeMode = ResizeMode.CanResizeWithGrip;
            }
            else
            {
                ResizeMode = ResizeMode.NoResize;
            }
            ChatPanel.Document = ViewModel.ChatDocument;

            _resizeEndTimer.Interval = TimeSpan.FromMilliseconds(3000);
            _resizeEndTimer.Tick += ResizeEndTimer_Tick;
        }
#pragma warning restore CS8602

#pragma warning disable CS8602
        private void ResizeEndTimer_Tick(object? sender, EventArgs e)
        {
            _resizeEndTimer.Stop();

            IronworksSettings.Instance.UiSettings.ChatWindowWidth = Width;
            IronworksSettings.Instance.UiSettings.ChatWindowHeight = Height;
        }
#pragma warning restore CS8602

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            _resizeEndTimer.Stop();
            _resizeEndTimer.Start();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            Hide();
        }

        protected override void OnContentRendered(EventArgs e)
        {
            base.OnContentRendered(e);

            //ViewModel.AddRandomMessage("Lorem ipsum dolor sit amet, consectetur adipiscing elit.");
        }
    }
}
