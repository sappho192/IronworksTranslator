using FontAwesome.WPF;
using IronworksTranslator.Core;
using IronworksTranslator.Util;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Xceed.Wpf.Toolkit;

namespace IronworksTranslator
{
    /// <summary>
    /// MainWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MainWindow : Window
    {
        private IronworksContext ironworksContext;
        private IronworksSettings ironworksSettings;
        //private 
        private readonly Timer chatboxTimer;

        public MainWindow()
        {
            Topmost = true;
            InitializeComponent();
            InitializeGeneralSettingsUI();

            var version = Assembly.GetExecutingAssembly().GetName().Version;
            mainWindow.Title += $" v{version}";
            AppHeaderLabel.Content += $" v{version}";
            Log.Information($"Current version: {version}");

            ironworksContext = IronworksContext.Instance();
            ironworksSettings = IronworksSettings.Instance;

            Welcome();
            LoadSettings();

            const int period = 500;
            chatboxTimer = new Timer(UpdateChatbox, null, 0, period);
            Log.Debug($"New RefreshChatbox timer with period {period}ms");
        }

        private void InitializeGeneralSettingsUI()
        {
            exampleChatBox.Text = $"이프 저격하는 무작위 레벨링 가실 분~{Environment.NewLine}エキルレ行く方いますか？{Environment.NewLine}Mechanics are for Cars KUPO!";
            var cond = System.Windows.Markup.XmlLanguage.GetLanguage(System.Globalization.CultureInfo.CurrentUICulture.Name);
            var listFont = new List<string>();
            foreach (FontFamily font in Fonts.SystemFontFamilies)
            {
                if (font.FamilyNames.ContainsKey(cond))
                    listFont.Add(font.FamilyNames[cond]);
                else
                    listFont.Add(font.ToString());
            }
            listFont.Sort();
            ChatFontFamilyComboBox.ItemsSource = listFont;

            foreach (string fontName in ChatFontFamilyComboBox.ItemsSource)
            {
                if (fontName.Equals(exampleChatBox.FontFamily.FamilyNames[cond]))
                {
                    ChatFontFamilyComboBox.SelectedValue = fontName;
                    break;
                }
            }
        }

        private void Welcome()
        {
            TranslatedChatBox.Text += $"최근에 강제종료했다면 작업관리자에서 PhantomJS.exe도 종료해주세요.{Environment.NewLine}";
            if (App.newcomer)
            //if (true)
            {
                TranslatedChatBox.Text += $"프로그램을 처음 쓰시는군요.{Environment.NewLine}";
                TranslatedChatBox.Text += $"메뉴 버튼을 눌러 채널별 언어 설정을 마무리해주세요.{Environment.NewLine}";
                ironworksSettings.UI.ChatTextboxFontFamily = ChatFontFamilyComboBox.SelectedValue as string;
            }
        }

        private void LoadSettings()
        {
            Log.Debug("Applying settings from file");
            LoadUISettings();
            LoadChatSettings();

            Log.Debug("Settings applied");
        }

        private void LoadUISettings()
        {
            chatFontSizeSpinner.Value = ironworksSettings.UI.ChatTextboxFontSize;
            TranslatedChatBox.FontSize = ironworksSettings.UI.ChatTextboxFontSize;

            ClientLanguageComboBox.SelectedIndex = (int)ironworksSettings.Translator.NativeLanguage;

            TranslatorEngineComboBox.SelectedIndex = (int)ironworksSettings.Translator.DefaultTranslatorEngine;

            ChatFontFamilyComboBox.SelectedValue = ironworksSettings.UI.ChatTextboxFontFamily;
            var font = new FontFamily(ironworksSettings.UI.ChatTextboxFontFamily);
            exampleChatBox.FontFamily = font;
            TranslatedChatBox.FontFamily = font;
        }

        private void Window_PreviewLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            Window window = (Window)sender;
            window.Topmost = true;
        }

        private void Window_PreviewTouchDown(object sender, TouchEventArgs e)
        {
            CaptureTouch(e.TouchDevice);
        }

        private void UpdateChatbox(object state)
        {
            if (ChatQueue.q.Any())
            {// Should q be locked?
                var chat = ChatQueue.q.Take();
                int.TryParse(chat.Code, System.Globalization.NumberStyles.HexNumber, null, out var intCode);
                ChatCode code = (ChatCode)intCode;
                if (code <= ChatCode.CWLinkShell8 && code != ChatCode.GilReceive && code != ChatCode.Gather)
                {// For now, CWLinkShell8(0x6B) is upper bound of chat related code. Gil received & Gather message will be ignored.
                    if (ironworksSettings.Chat.ChannelVisibility.TryGetValue(code, out bool show))
                    {
                        if (show)
                        {
                            Log.Debug("Chat: {@Chat}", chat);
                            var autoTranslates = chat.Bytes.ExtractAutoTranslate();
                            if(autoTranslates.Count != 0)
                            {
                                throw new Exception("AutoTranslate found");
                            }

                            if (code == ChatCode.Recruitment || code == ChatCode.System || code == ChatCode.Error
                                || code == ChatCode.Notice || code == ChatCode.Emote || code == ChatCode.MarketSold)
                            {
                                if (!ContainsNativeLanguage(chat.Line))
                                {
                                    var translated = ironworksContext.TranslateChat(chat.Line, ironworksSettings.Chat.ChannelLanguage[code]);

                                    Application.Current.Dispatcher.Invoke(() =>
                                    {
                                        TranslatedChatBox.Text +=
#if DEBUG
                                        $"[{chat.Code}]{translated}{Environment.NewLine}";
#else
                                        $"{translated}{Environment.NewLine}";
#endif
                                    });
                                }
                            }
                            else
                            {
                                var author = chat.Line.RemoveAfter(":");
                                var sentence = chat.Line.RemoveBefore(":");
                                if (!ContainsNativeLanguage(chat.Line))
                                {
                                    var translated = ironworksContext.TranslateChat(sentence, ironworksSettings.Chat.ChannelLanguage[code]);

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
                            }
                        }
                    }
                    else
                    {
                        Log.Warning("UNEXPECTED CHATCODE {@Code} when translating {@Message}", intCode, chat.Line);
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            TranslatedChatBox.Text +=
                        $"[모르는 채널-제보요망][{chat.Code}]{chat.Line}{Environment.NewLine}";
                        });
                    }
                }
                else
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        TranslatedChatBox.Text +=
#if DEBUG
                            $"[???][{chat.Code}]{chat.Line}{Environment.NewLine}";
#else
                            $"[???]{chat.Line}{Environment.NewLine}";
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

        private bool ContainsNativeLanguage(string sentence)
        {
            switch (ironworksSettings.Translator.NativeLanguage)
            {
                case ClientLanguage.Japanese:
                    return sentence.HasJapanese();
                case ClientLanguage.English:
                    return sentence.HasEnglish();
                case ClientLanguage.Korean:
                    return sentence.HasKorean();
                case ClientLanguage.German:
                case ClientLanguage.French:
                default:
                    return false;
            }
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            if (System.Windows.MessageBox.Show("정말 종료할까요?", "프로그램 종료", MessageBoxButton.YesNo).Equals(MessageBoxResult.Yes))
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
            ShowOnly(UI.SettingsTab.None); // Hide all settings grid
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.MainWindow.WindowState = System.Windows.WindowState.Minimized;
        }

        private void GeneralSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleSettingsGrid(GeneralSettingsGrid, UI.SettingsTab.General);
        }

        private void LanguageSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleSettingsGrid(LanguageSettingsGrid, UI.SettingsTab.Language);
        }

        private void ChatSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleSettingsGrid(ChatSettingsGrid, UI.SettingsTab.Chat);
        }

        private void ToggleSettingsGrid(Grid settingsGrid, UI.SettingsTab setting)
        {
            if (settingsGrid.Visibility.Equals(Visibility.Hidden))
            {
                ShowOnly(setting);
            }
            else
            {
                settingsGrid.Visibility = Visibility.Hidden;
            }
        }

        private void ShowOnly(UI.SettingsTab active)
        {
            switch (active)
            {
                case UI.SettingsTab.General:
                    GeneralSettingsGrid.Visibility = Visibility.Visible;
                    LanguageSettingsGrid.Visibility = Visibility.Hidden;
                    ChatSettingsGrid.Visibility = Visibility.Hidden;
                    break;
                case UI.SettingsTab.Language:
                    GeneralSettingsGrid.Visibility = Visibility.Hidden;
                    LanguageSettingsGrid.Visibility = Visibility.Visible;
                    ChatSettingsGrid.Visibility = Visibility.Hidden;
                    break;
                case UI.SettingsTab.Chat:
                    GeneralSettingsGrid.Visibility = Visibility.Hidden;
                    LanguageSettingsGrid.Visibility = Visibility.Hidden;
                    ChatSettingsGrid.Visibility = Visibility.Visible;
                    break;
                case UI.SettingsTab.None:
                    GeneralSettingsGrid.Visibility = Visibility.Hidden;
                    LanguageSettingsGrid.Visibility = Visibility.Hidden;
                    ChatSettingsGrid.Visibility = Visibility.Hidden;
                    break;
            }
        }

        private void ChatFontSizeSpinner_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            IntegerUpDown spinner = sender as IntegerUpDown;
            if (ironworksSettings != null)
            {
                ironworksSettings.UI.ChatTextboxFontSize = spinner.Value ?? 6;
                TranslatedChatBox.FontSize = ironworksSettings.UI.ChatTextboxFontSize;
                exampleChatBox.FontSize = ironworksSettings.UI.ChatTextboxFontSize;
            }
        }

        private void ClientLanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ironworksSettings != null)
            {
                ComboBox box = sender as ComboBox;
                ironworksSettings.Translator.NativeLanguage = (ClientLanguage)box.SelectedIndex;
            }
        }

        private void TranslatorEngineComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ironworksSettings != null)
            {
                ComboBox box = sender as ComboBox;
                ironworksSettings.Translator.DefaultTranslatorEngine = (TranslatorEngine)box.SelectedIndex;
            }
        }

        private void ChangeMajorLanguage(Settings.Channel channel, ClientLanguage language)
        {
            if (ironworksSettings != null)
            {
                channel.MajorLanguage = language;
            }
        }

        private void ChatFontFamilyComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ironworksSettings != null)
            {
                var box = sender as ComboBox;
                ironworksSettings.UI.ChatTextboxFontFamily = box.SelectedItem as string;
                var font = new FontFamily(ironworksSettings.UI.ChatTextboxFontFamily);
                exampleChatBox.FontFamily = font;
                TranslatedChatBox.FontFamily = font;
            }
        }
    }
}
