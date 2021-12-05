using FontAwesome.WPF;
using IronworksTranslator.Core;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace IronworksTranslator
{
    /// <summary>
    /// DialogueWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class DialogueWindow : Window
    {
        private IronworksContext ironworksContext;
        private IronworksSettings ironworksSettings;
        private readonly Timer chatboxTimer;
        private static Regex regexItem = new Regex(@"&\u0003(.*)\u0002I\u0002");
        private bool isUIInitialized = false;

        public DialogueWindow(MainWindow mainWindow)
        {
            Topmost = true;
            InitializeComponent();
            isUIInitialized = true;
            ironworksContext = mainWindow.ironworksContext;
            ironworksSettings = mainWindow.ironworksSettings;
            LoadUISettings();

            const int period = 500;
            chatboxTimer = new Timer(RefreshDialogueTextBox, null, 0, period);
            Log.Debug($"New RefreshChatbox timer with period {period}ms");
        }

        private void RefreshDialogueTextBox(object state)
        {
            if (ChatQueue.rq.Any())
            {
                var result = ChatQueue.rq.TryDequeue(out string msg);
                if(ironworksSettings.Translator.DefaultDialogueTranslationMethod == DialogueTranslationMethod.MemorySearch)
                {
                    if (result)
                    {
                        msg = Regex.Replace(msg, @"\uE03C", "[HQ]");
                        msg = Regex.Replace(msg, @"\uE06F", "⇒");
                        msg = Regex.Replace(msg, @"\uE0BB", string.Empty);
                        msg = Regex.Replace(msg, @"\uFFFD", string.Empty);
                        if (msg.IndexOf('\u0002') == 0)
                        {
                            var filter = regexItem.Match(msg);
                            if (filter.Success)
                            {
                                msg = filter.Groups[1].Value;
                            }
                        }
                        if (!msg.Equals(string.Empty))
                        {
                            var translated = ironworksContext.TranslateChat(msg, ironworksSettings.Translator.DialogueLanguage);

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

        public void PushDialogueTextBox(string dialogue)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                DialogueTextBox.Text += $"{Environment.NewLine}{dialogue}";
                DialogueTextBox.ScrollToEnd();
            });
        }

        private void LoadUISettings()
        {
            ContentBackgroundGrid.Opacity = ironworksSettings.UI.DialogueBackgroundOpacity;
            ContentOpacitySlider.Value = ironworksSettings.UI.DialogueBackgroundOpacity;

            if (ironworksSettings.UI.DialogueWindowPosTop < 0 ||
    ironworksSettings.UI.DialogueWindowPosTop > SystemParameters.PrimaryScreenHeight)
            {
                ironworksSettings.UI.DialogueWindowPosTop = 100;
            }
            dialogueWindow.Top = ironworksSettings.UI.DialogueWindowPosTop;
            if (ironworksSettings.UI.DialogueWindowPosLeft < 0 ||
                ironworksSettings.UI.DialogueWindowPosLeft > SystemParameters.PrimaryScreenWidth)
            {
                ironworksSettings.UI.DialogueWindowPosLeft = 100;
            }
            dialogueWindow.Left = ironworksSettings.UI.DialogueWindowPosLeft;

            var font = new FontFamily(ironworksSettings.UI.ChatTextboxFontFamily);
            DialogueTextBox.FontFamily = font;
            DialogueTextBox.FontSize = ironworksSettings.UI.ChatTextboxFontSize;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Visibility = Visibility.Hidden;
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
            this.WindowState = WindowState.Minimized;
        }

        private void ContentOpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (ironworksSettings != null)
            {
                var slider = sender as Slider;
                ChangeBackgroundOpacity(slider.Value);
            }
        }

        private void ChangeBackgroundOpacity(double opacity)
        {
            ContentBackgroundGrid.Opacity = opacity;
            ironworksSettings.UI.DialogueBackgroundOpacity = opacity;
        }

        private void ContentOpacitySlider_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ironworksSettings != null)
            {
                var slider = sender as Slider;
                const double ORIGINAL = 0.75;
                slider.Value = ORIGINAL;
                ChangeBackgroundOpacity(ORIGINAL);
            }
        }

        private void ShowContentBackground_Click(object sender, RoutedEventArgs e)
        {
            ContentOpacitySlider.Value = 1;
            ChangeBackgroundOpacity(1);
        }

        private void HideContentBackground_Click(object sender, RoutedEventArgs e)
        {
            ContentOpacitySlider.Value = 0;
            ChangeBackgroundOpacity(0);
        }

        private void dialogueWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (ironworksSettings != null)
            {
                var window = sender as Window;
                ironworksSettings.UI.DialogueWindowWidth = window.Width;
                ironworksSettings.UI.DialogueWindowHeight = window.Height;
            }
            DialogueTextBox.ScrollToEnd();
        }

        private void MaskGrid_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            DialogueTextBox.RaiseEvent(e);
        }

        private void dialogueWindow_LocationChanged(object sender, EventArgs e)
        {
            if(isUIInitialized)
            {
                if (dialogueWindow.WindowState == System.Windows.WindowState.Normal)
                {
                    if (dialogueWindow.Top > 0 && dialogueWindow.Top < SystemParameters.PrimaryScreenHeight)
                    {
                        ironworksSettings.UI.DialogueWindowPosTop = dialogueWindow.Top;
                    }
                    if (dialogueWindow.Left > 0 && dialogueWindow.Left < SystemParameters.PrimaryScreenWidth)
                    {
                        ironworksSettings.UI.DialogueWindowPosLeft = dialogueWindow.Left;
                    }
                }
            }
        }
    }
}
