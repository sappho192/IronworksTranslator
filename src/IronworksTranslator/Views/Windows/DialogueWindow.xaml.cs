using IronworksTranslator.Models.Enums;
using IronworksTranslator.Models;
using IronworksTranslator.Models.Settings;
using IronworksTranslator.ViewModels.Windows;
using Serilog;
using System.Text.RegularExpressions;
using System.Windows.Threading;
using WpfScreenHelper;
using IronworksTranslator.Utils.Translator;
using IronworksTranslator.Utils.UI;

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

        private readonly Timer chatboxTimer;
        private const int period = 500;
        private static readonly Regex regexItem = MyRegex();
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
            chatboxTimer = new Timer(RefreshDialogueTextBox, null, 0, period);
            Log.Debug($"New RefreshChatbox timer with period {period}ms");
        }

        private void RefreshDialogueTextBox(object? state)
        {
            if (!ChatQueue.rq.IsEmpty)
            {
                var result = ChatQueue.rq.TryDequeue(out string? msg);
                if (msg == null) return;
                if (IronworksSettings.Instance.TranslatorSettings.DialogueTranslationMethod == DialogueTranslationMethod.MemorySearch)
                {
                    if (result)
                    {
                        msg = MyRegex1().Replace(msg, "[HQ]");
                        msg = MyRegex2().Replace(msg, "⇒");
                        msg = MyRegex3().Replace(msg, string.Empty);
                        msg = MyRegex4().Replace(msg, string.Empty);
                        if (msg.StartsWith('\u0002'))
                        {
                            var filter = regexItem.Match(msg);
                            if (filter.Success)
                            {
                                msg = filter.Groups[1].Value;
                            }
                        }
                        if (!msg.Equals(string.Empty))
                        {
                            var translated = Translate(msg, IronworksSettings.Instance.ChannelSettings.NpcDialog.MajorLanguage);

                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                DialogueTextBox.Text += $"{Environment.NewLine}{translated}";
                                DialogueTextBox.ScrollToEnd();
                            });
                        }
                    }
                }
            }
        }

        public void PushDialogueTextBox(string? dialogue)
        {
            if (dialogue == null) return;
            Application.Current.Dispatcher.Invoke(() =>
            {
                DialogueTextBox.Text += $"{Environment.NewLine}{dialogue}";
                DialogueTextBox.ScrollToEnd();
            });
        }

        private string Translate(string input, ClientLanguage channelLanguage, TranslatorEngine? translatorEngine = null)
        {
            Log.Information($"Translating {input}");
            string result = string.Empty;
            var switcher = translatorEngine ?? IronworksSettings.Instance.TranslatorSettings.TranslatorEngine;
            switch (switcher)
            {
                case TranslatorEngine.Papago:
                    result = App.GetService<PapagoTranslator>().Translate(
                            input,
                            (TranslationLanguageCode)channelLanguage,
                            (TranslationLanguageCode)IronworksSettings.Instance.TranslatorSettings.ClientLanguage
                        );
                    break;
                case TranslatorEngine.DeepL_API:
                    result = App.GetService<DeepLAPITranslator>().Translate(
                            input,
                            (TranslationLanguageCode)channelLanguage,
                            (TranslationLanguageCode)IronworksSettings.Instance.TranslatorSettings.ClientLanguage
                        );
                    break;
                case TranslatorEngine.Ironworks_Ja_Ko:
                    result = input;
                    break;
                default:
                    break;
            }
            Log.Information($"Translated {input}");
            return result;
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

        [GeneratedRegex(@"&\u0003(.*)\u0002I\u0002")]
        private static partial Regex MyRegex();
        [GeneratedRegex(@"\uE03C")]
        private static partial Regex MyRegex1();
        [GeneratedRegex(@"\uE06F")]
        private static partial Regex MyRegex2();
        [GeneratedRegex(@"\uE0BB")]
        private static partial Regex MyRegex3();
        [GeneratedRegex(@"\uFFFD")]
        private static partial Regex MyRegex4();
#pragma warning restore CS8602
    }
}
