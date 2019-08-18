using IronworksTranslator.Core;
using Sharlayan.Core;
using Serilog;
using System;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using Serilog.Events;
using Serilog.Formatting.Compact;

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
            {
                var chat = ChatQueue.q.Take();
                //ChatQueue.q.TryDequeue(out chat);
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
                    //TranslatedChatBox.ScrollToEnd();
                    TranslatedChatBox.ScrollToVerticalOffset(double.MaxValue);
                });
            }
        }
    }
}
