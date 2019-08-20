using FontAwesome.WPF;
using IronworksTranslator.Core;
using Serilog;
using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace IronworksTranslator
{
    /// <summary>
    /// MainWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MainWindow : Window
    {
        private IronworksContext ironworksContext;
        //private 
        private readonly Timer chatboxTimer;

        public MainWindow()
        {
            Topmost = true;
            InitializeComponent();

            var version = Assembly.GetExecutingAssembly().GetName().Version;
            mainWindow.Title += $" v{version}";
            Log.Information($"Current version: {version}");

            ironworksContext = IronworksContext.Instance();

            const int period = 500;
            chatboxTimer = new Timer(RefreshChatbox, null, 0, period);
            Log.Debug($"New RefreshChatbox timer with period {period}ms");
        }

        private void RefreshChatbox(object state)
        {
            UpdateChatbox();
        }

        private void Window_PreviewLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            Window window = (Window)sender;
            window.Topmost = true;
        }

        private void UpdateChatbox()
        {
            if (ChatQueue.q.Any())
            {// Should q be locked?
                var chat = ChatQueue.q.Take();
                int.TryParse(chat.Code, System.Globalization.NumberStyles.HexNumber, null, out var code);
                if (code <= 0x30)
                {
                    Log.Debug("Chat: {@Chat}",chat);
                    var author = chat.Line.RemoveAfter(":");
                    var sentence = chat.Line.RemoveBefore(":");
                    var translated = ironworksContext.TranslateChat(sentence);

                    Application.Current.Dispatcher.Invoke(() =>
                    {

                        TranslatedChatBox.Text +=
#if DEBUG
                        $"[{chat.Code}]{author}:{translated}{Environment.NewLine}";
#else
                        $"{author}:{translated}{Environment.NewLine}";
#endif
                    });
                }
                else
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        TranslatedChatBox.Text +=
#if DEBUG
                            $"[{chat.Code}]{chat.Line}{Environment.NewLine}";
#else
                            $"{chat.Line}{Environment.NewLine}";
#endif
                    });
                }
                Application.Current.Dispatcher.Invoke(() =>
                {
                    TranslatedChatBox.ScrollToEnd();
                    //TranslatedChatBox.ScrollToVerticalOffset(double.MaxValue);
                });
            }
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            if(MessageBox.Show("정말 종료할까요?", "프로그램 종료", MessageBoxButton.YesNo).Equals(MessageBoxResult.Yes))
            {
                Application.Current.Shutdown();
            }
        }

        private void CollapseButton_Click(object sender, RoutedEventArgs e)
        {
            var icon = (sender as Button).Content as ImageAwesome;
            icon.Icon =
                icon.Icon.Equals(FontAwesomeIcon.Bars) ?
                FontAwesomeIcon.AngleDoubleUp
                : FontAwesomeIcon.Bars;
            ToolbarGrid.Visibility =
                ToolbarGrid.Visibility.Equals(Visibility.Collapsed) ?
                 Visibility.Visible : Visibility.Collapsed;
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.MainWindow.WindowState = WindowState.Minimized;
        }
    }
}
