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
        }

        [ObservableProperty]
        [property: YamlMember(Alias = "theme")]
        private ApplicationTheme _theme;

        [ObservableProperty]
        [property: YamlMember(Alias = "app_language")]
        private AppLanguage _appLanguage;

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
    }
}
