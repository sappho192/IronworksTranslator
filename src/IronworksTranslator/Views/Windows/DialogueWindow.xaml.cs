using IronworksTranslator.Models.Settings;
using IronworksTranslator.ViewModels.Windows;
using System.Windows.Threading;

namespace IronworksTranslator.Views.Windows
{
    /// <summary>
    /// DialogueWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class DialogueWindow : Window
    {
        public DialogueWindowViewModel ViewModel { get; }

        private readonly DispatcherTimer _resizeEndTimer = new();

        public DialogueWindow(DialogueWindowViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = this;

            InitializeComponent();

#pragma warning disable CS8602
            if (IronworksSettings.Instance.UiSettings.DialogueWindowVisible)
            {
                Show();
            }
            else
            {
                Hide();
            }
            if (IronworksSettings.Instance.ChatUiSettings.IsResizable)
            {
                ResizeMode = ResizeMode.CanResizeWithGrip;
            } else
            {
                ResizeMode = ResizeMode.NoResize;
            }

            _resizeEndTimer.Interval = TimeSpan.FromMilliseconds(3000);
            _resizeEndTimer.Tick += ResizeEndTimer_Tick;
        }

        private void ResizeEndTimer_Tick(object? sender, EventArgs e)
        {
            _resizeEndTimer.Stop();

            IronworksSettings.Instance.UiSettings.DialogueWindowWidth = Width;
            IronworksSettings.Instance.UiSettings.DialogueWindowHeight = Height;
        }
#pragma warning restore CS8602

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            Hide();
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            _resizeEndTimer.Stop();
            _resizeEndTimer.Start();
        }
    }
}
