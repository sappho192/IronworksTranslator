using IronworksTranslator.Core;
using Sharlayan.Core;
using System;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows;
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
            ironworksContext = IronworksContext.Instance();

            chatboxTimer = new Timer(RefreshChatbox, null, 0, 1000);
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
                ChatLogItem chat = ChatQueue.q.Take();
                //ChatQueue.q.TryDequeue(out chat);
                int.TryParse(chat.Code, System.Globalization.NumberStyles.HexNumber, null, out var code);
                StringBuilder stringBuilder = new StringBuilder();
                if (code <= 0x30)
                {
                    var author = chat.Line.RemoveAfter(":");
                    var sentence = chat.Line.RemoveBefore(":");
                    var translated = ironworksContext.TranslateChat(sentence);

#if DEBUG
                    stringBuilder.Append(chat.Code).Append(author).Append(":").Append(translated).Append(Environment.NewLine);
#else
                    stringBuilder.Append(author).Append(":").Append(translated).Append(Environment.NewLine);
#endif

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        TranslatedChatBox.Text += stringBuilder.ToString();
                    });

                    stringBuilder.Clear();
                }
                else
                {
#if DEBUG
                    stringBuilder.Append(chat.Code).Append(chat.Line).Append(Environment.NewLine);
#else
                    stringBuilder.Append(chat.Line).Append(Environment.NewLine);
#endif
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        TranslatedChatBox.Text += stringBuilder.ToString();
                    });
                    stringBuilder.Clear();
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
