﻿using FontAwesome.WPF;
using IronworksTranslator.Core;
using Serilog;
using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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

            var version = Assembly.GetExecutingAssembly().GetName().Version;
            mainWindow.Title += $" v{version}";
            Log.Information($"Current version: {version}");

            ironworksContext = IronworksContext.Instance();
            ironworksSettings = IronworksSettings.Instance;
            LoadSettings();

            const int period = 500;
            chatboxTimer = new Timer(RefreshChatbox, null, 0, period);
            Log.Debug($"New RefreshChatbox timer with period {period}ms");
        }

        private void LoadSettings()
        {
            Log.Debug("Applying settings from file");
            chatFontSizeSpinner.Value = ironworksSettings.UI.ChatTextboxFontSize;
            TranslatedChatBox.FontSize = ironworksSettings.UI.ChatTextboxFontSize;

            ClientLanguageComboBox.SelectedIndex = (int)ironworksSettings.Translator.NativeLanguage;

            TranslatorEngineComboBox.SelectedIndex = (int)ironworksSettings.Translator.DefaultTranslatorEngine;

            LoadChatSettings();

            Log.Debug("Settings applied");
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

        private void Window_PreviewTouchDown(object sender, TouchEventArgs e)
        {
            CaptureTouch(e.TouchDevice);
        }

        private void UpdateChatbox()
        {
            if (ChatQueue.q.Any())
            {// Should q be locked?
                var chat = ChatQueue.q.Take();
                int.TryParse(chat.Code, System.Globalization.NumberStyles.HexNumber, null, out var intCode);
                ChatCode code = (ChatCode)intCode;
                if (code <= ChatCode.Recruitment) // 0x48, 0d72
                {
                    if (ironworksSettings.Chat.ChannelVisibility.TryGetValue(code, out bool show))
                    {
                        if(show)
                        {
                            Log.Debug("Chat: {@Chat}", chat);
                            if (code == ChatCode.Recruitment || code == ChatCode.System || code == ChatCode.Error)
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
                            else
                            {
                                var author = chat.Line.RemoveAfter(":");
                                var sentence = chat.Line.RemoveBefore(":");
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
                    else
                    {
                        Log.Error("UNEXPECTED CHATCODE {@Code} when translating {@Message}", intCode, chat.Line);
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
    }
}
