using IronworksTranslator.Models.Settings;
using IronworksTranslator.Utils.UI;
using IronworksTranslator.ViewModels.Windows;
using System.Windows.Threading;
using WpfScreenHelper;

namespace IronworksTranslator.Views.Windows
{
    /// <summary>
    /// ChatWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class ChatWindow : Window
    {
        public ChatWindowViewModel ViewModel { get; }
        private readonly DispatcherTimer _resizeEndTimer = new();
        private readonly DispatcherTimer _repositionEndTimer = new();
        private readonly bool _isUiInitialized = false;

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
            _repositionEndTimer.Interval = TimeSpan.FromMilliseconds(3000);
            _repositionEndTimer.Tick += RepositionEndTimer_Tick;

            _isUiInitialized = true;
        }

#pragma warning restore CS8602

#pragma warning disable CS8602
        private void RepositionEndTimer_Tick(object? sender, EventArgs e)
        {
            _repositionEndTimer.Stop();

            if (_isUiInitialized) 
            {
                var top = mainWindow.Top;
                var left = mainWindow.Left;

                if (WindowChecker.IsMinimized(top, left) || WindowChecker.IsMaximized(top, left))
                {
                    return;
                }

                if (mainWindow.WindowState.Equals(WindowState.Normal))
                {
                    IronworksSettings.Instance.UiSettings.ChatWindowTop = top;
                    IronworksSettings.Instance.UiSettings.ChatWindowLeft = left;
                    IronworksSettings.Instance.UiSettings.ChatWindowScreen = Screen.FromWindow(mainWindow).DeviceName;
                }
            }
        }

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

        private void Window_LocationChanged(object sender, EventArgs e)
        {
            _repositionEndTimer.Stop();
            _repositionEndTimer.Start();
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

#pragma warning disable CS8602
        private void Window_ContentRendered(object sender, EventArgs e)
        {
            double windowTop = IronworksSettings.Instance.UiSettings.ChatWindowTop;
            double windowLeft = IronworksSettings.Instance.UiSettings.ChatWindowLeft;
            string? deviceName = IronworksSettings.Instance.UiSettings.ChatWindowScreen;
            if (windowTop != 0 && windowLeft != 0)
            {
                var mainScreen = Screen.AllScreens.Where(screen => screen.DeviceName.Equals(deviceName)).FirstOrDefault();
                mainScreen ??= Screen.PrimaryScreen;

                WindowHelper.SetWindowPosition(mainWindow, WpfScreenHelper.Enum.WindowPositions.Center, mainScreen);
                mainWindow.Left = windowLeft;
                mainWindow.Top = windowTop;
            }
        }
#pragma warning restore CS8602
    }
}
