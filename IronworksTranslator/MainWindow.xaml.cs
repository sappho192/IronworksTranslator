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
        private BackgroundWorker chatboxUpdater;

        public MainWindow()
        {
            Topmost = true;
            InitializeComponent();
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            mainWindow.Title += $" v{version.ToString()}";
            ironworksContext = IronworksContext.Instance();
            chatboxUpdater = new BackgroundWorker
            {
                WorkerReportsProgress = true,
                WorkerSupportsCancellation = true
            };
            chatboxUpdater.DoWork += ChatboxUpdater_DoWork;
            chatboxUpdater.ProgressChanged += ChatboxUpdater_ProgressChanged;
            chatboxUpdater.RunWorkerAsync();
        }

        private void ChatboxUpdater_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {

        }

        private void ChatboxUpdater_DoWork(object sender, DoWorkEventArgs e)
        {
            var updater = sender as BackgroundWorker;
            while (!updater.CancellationPending)
            {
                Thread.Sleep(1000);
                UpdateChatbox();
                updater.ReportProgress(0);
            }
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
                    //stringBuilder.Append(chat.Code).Append(author).Append(":").Append(translated).Append(Environment.NewLine);
                    stringBuilder.Append(author).Append(":").Append(translated).Append(Environment.NewLine);
                    Application.Current.Dispatcher.Invoke(new Action(() =>
                    {
                        TranslatedChatBox.Text += stringBuilder.ToString();
                    }));

                    stringBuilder.Clear();
                }
                else
                {
                    stringBuilder.Append(chat.Line).Append(Environment.NewLine);
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
