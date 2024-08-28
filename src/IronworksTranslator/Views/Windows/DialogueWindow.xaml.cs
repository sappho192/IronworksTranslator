using IronworksTranslator.Models.Settings;
using IronworksTranslator.Utils;
using IronworksTranslator.ViewModels.Windows;
using System.Windows.Threading;
using WpfScreenHelper;

namespace IronworksTranslator.Views.Windows
{
    /// <summary>
    /// DialogueWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class DialogueWindow : Window
    {
        public DialogueWindowViewModel ViewModel { get; }

        private readonly DispatcherTimer _resizeEndTimer = new();
        private readonly DispatcherTimer _repositionEndTimer = new();
        private readonly bool _isUiInitialized = false;

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
            _repositionEndTimer.Interval = TimeSpan.FromMilliseconds(3000);
            _repositionEndTimer.Tick += RepositionEndTimer_Tick;

            _isUiInitialized = true;
        }

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
                    IronworksSettings.Instance.UiSettings.DialogueWindowTop = top;
                    IronworksSettings.Instance.UiSettings.DialogueWindowLeft = left;
                    IronworksSettings.Instance.UiSettings.DialogueWindowScreen = Screen.FromWindow(mainWindow).DeviceName;
                }
            }
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

        private void Window_LocationChanged(object sender, EventArgs e)
        {
            _repositionEndTimer.Stop();
            _repositionEndTimer.Start();
        }

#pragma warning disable CS8602
        private void Window_ContentRendered(object sender, EventArgs e)
        {
            double windowTop = IronworksSettings.Instance.UiSettings.DialogueWindowTop;
            double windowLeft = IronworksSettings.Instance.UiSettings.DialogueWindowLeft;
            string? deviceName = IronworksSettings.Instance.UiSettings.DialogueWindowScreen;
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
