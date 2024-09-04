using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using Serilog;
using Wpf.Ui.Appearance;
using YamlDotNet.Serialization;
using IronworksTranslator.Utils;
using IronworksTranslator.ViewModels.Pages;
using IronworksTranslator.Models.Enums;

namespace IronworksTranslator.Models.Settings
{
    public partial class UISettings : ObservableRecipient
    {
        public UISettings()
        {
            Messenger.Register<PropertyChangedMessage<ApplicationTheme>>(this, OnThemeMessage);
            Messenger.Register<PropertyChangedMessage<AppLanguage>>(this, OnAppLanguageMessage);
            Messenger.Register<PropertyChangedMessage<bool>>(this, OnBoolMessage);
        }

        [ObservableProperty]
        [property: YamlMember(Alias = "is_tos_displayed")]
        private bool _isTosDisplayed;

        [ObservableProperty]
        [property: YamlMember(Alias = "theme")]
        private ApplicationTheme _theme;

        [ObservableProperty]
        [property: YamlMember(Alias = "app_language")]
        private AppLanguage _appLanguage;

        [ObservableProperty]
        [property: YamlMember(Alias = "dialogue_window_visible")]
        private bool _dialogueWindowVisible;

        [ObservableProperty]
        [property: YamlMember(Alias = "chat_window_width")]
        private double _chatWindowWidth;

        [ObservableProperty]
        [property: YamlMember(Alias = "chat_window_height")]
        private double _chatWindowHeight;

        [ObservableProperty]
        [property: YamlMember(Alias = "dialogue_window_width")]
        private double _dialogueWindowWidth;

        [ObservableProperty]
        [property: YamlMember(Alias = "dialogue_window_height")]
        private double _dialogueWindowHeight;

        [ObservableProperty]
        [property: YamlMember(Alias = "chat_window_top")]
        private double _chatWindowTop;

        [ObservableProperty]
        [property: YamlMember(Alias = "chat_window_left")]
        private double _chatWindowLeft;

        [ObservableProperty]
        [property: YamlMember(Alias = "dialogue_window_top")]
        private double _dialogueWindowTop;

        [ObservableProperty]
        [property: YamlMember(Alias = "dialogue_window_left")]
        private double _dialogueWindowLeft;

        [ObservableProperty]
        [property: YamlMember(Alias = "chat_window_screen")]
        private string? _chatWindowScreen;

        [ObservableProperty]
        [property: YamlMember(Alias = "dialogue_window_screen")]
        private string? _dialogueWindowScreen;

        [SaveSettingsOnChange]
        partial void OnIsTosDisplayedChanged(bool value)
        {
            Log.Information($"User have agreed to the terms of service.");
        }

        [SaveSettingsOnChange]
        partial void OnThemeChanged(ApplicationTheme value)
        {
            Log.Information($"Theme changed to {value}");
        }

        private void OnThemeMessage(object s, PropertyChangedMessage<ApplicationTheme> m)
        {
            switch (m.PropertyName)
            {
                case nameof(SettingsViewModel.CurrentTheme):
                    Theme = m.NewValue;
                    break;
            }
        }

        [SaveSettingsOnChange]
        partial void OnAppLanguageChanged(AppLanguage value)
        {
            Log.Information($"AppLanguage changed to {value}");
        }

        private void OnAppLanguageMessage(object s, PropertyChangedMessage<AppLanguage> m)
        {
            switch (m.PropertyName)
            {
                case nameof(SettingsViewModel.AppLanguage):
                    AppLanguage = m.NewValue;
                    break;
            }
        }

        [SaveSettingsOnChange]
        partial void OnDialogueWindowVisibleChanged(bool value)
        {
            Log.Information($"DialogueWindowVisible changed to {value}");
        }

        private void OnBoolMessage(object recipient, PropertyChangedMessage<bool> m)
        {
            switch (m.PropertyName)
            {
                case nameof(DashboardViewModel.IsDialogueWindowVisible):
                    DialogueWindowVisible = m.NewValue;
                    break;
            }
        }

        [SaveSettingsOnChange]
        partial void OnChatWindowWidthChanged(double value)
        {
            Log.Information($"ChatWindowWidth changed to {value}");
        }

        [SaveSettingsOnChange]
        partial void OnChatWindowHeightChanged(double value)
        {
            Log.Information($"ChatWindowHeight changed to {value}");
        }

        [SaveSettingsOnChange]
        partial void OnDialogueWindowWidthChanged(double value)
        {
            Log.Information($"DialogueWindowWidth changed to {value}");
        }

        [SaveSettingsOnChange]
        partial void OnDialogueWindowHeightChanged(double value)
        {
            Log.Information($"DialogueWindowHeight changed to {value}");
        }

        [SaveSettingsOnChange]
        partial void OnChatWindowTopChanged(double value)
        {
            Log.Information($"ChatWindowTop changed to {value}");
        }

        [SaveSettingsOnChange]
        partial void OnChatWindowLeftChanged(double value)
        {
            Log.Information($"ChatWindowLeft changed to {value}");
        }

        [SaveSettingsOnChange]
        partial void OnDialogueWindowTopChanged(double value)
        {
            Log.Information($"DialogueWindowTop changed to {value}");
        }

        [SaveSettingsOnChange]
        partial void OnDialogueWindowLeftChanged(double value)
        {
            Log.Information($"DialogueWindowLeft changed to {value}");
        }

        [SaveSettingsOnChange]
        partial void OnChatWindowScreenChanged(string? value)
        {
            Log.Information($"ChatWindowScreen changed to {value}");
        }

        [SaveSettingsOnChange]
        partial void OnDialogueWindowScreenChanged(string? value)
        {
            Log.Information($"DialogueWindowScreen changed to {value}");
        }
    }
}
