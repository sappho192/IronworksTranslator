using IronworksTranslator.Core;
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
        private StringBuilder stringBuilder = new StringBuilder();
        private readonly Timer chatboxTimer;

        public MainWindow()
        {
            Topmost = true;
            InitializeComponent();
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            mainWindow.Title += $" v{version.ToString()}";
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

        private void UpdateChatButton_Click(object sender, RoutedEventArgs e)
        {
            Update();
        }

        private void Update()
        {
            ironworksContext.UpdateChat();
            UpdateChatbox();
        }

        private void UpdateChatbox()
        {
            int code = 0xA;
            while (ChatQueue.q.Any())
            {
                var chat = ChatQueue.q.Dequeue();
                int.TryParse(chat.Code, System.Globalization.NumberStyles.HexNumber, null, out code);
                if (code <= 0x30)
                {
                    var author = chat.Line.RemoveAfter(":");
                    var sentence = chat.Line.RemoveBefore(":");
                    var translated = ironworksContext.TranslateChat(sentence);

                    /* ONLY FOR DEBUG */
                    stringBuilder.Append(chat.Code).Append(author).Append(":").Append(translated).Append(Environment.NewLine);
                    
                    /* PRODUCTION */
                    //stringBuilder.Append(author).Append(":").Append(translated).Append(Environment.NewLine);
                    Application.Current.Dispatcher.Invoke(new Action(() =>
                    {
                        TranslatedChatBox.Text += stringBuilder.ToString();
                    }));

                    stringBuilder.Clear();
                }
                else
                {
                    /* ONLY FOR DEBUG */
                    stringBuilder.Append(chat.Code).Append(chat.Line).Append(Environment.NewLine);

                    /* PRODUCTION */
                    //stringBuilder.Append(chat.Line).Append(Environment.NewLine);

                    Application.Current.Dispatcher.Invoke(new Action(() =>
                    {
                        TranslatedChatBox.Text += stringBuilder.ToString();
                    }));
                    stringBuilder.Clear();
                }
            }
            Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                TranslatedChatBox.ScrollToEnd();
            }));
        }
    }
}
