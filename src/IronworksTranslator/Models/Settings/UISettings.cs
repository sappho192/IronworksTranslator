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
    }
}
