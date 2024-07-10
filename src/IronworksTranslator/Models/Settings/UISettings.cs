using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using Serilog;
using Wpf.Ui.Appearance;
using YamlDotNet.Serialization;
using IronworksTranslator.Utils;

namespace IronworksTranslator.Models.Settings
{
    public partial class UISettings : ObservableRecipient
    {
        public UISettings()
        {
            Messenger.Register<PropertyChangedMessage<ApplicationTheme>>(this, OnThemeMessage);
        }

        [ObservableProperty]
        [property: YamlMember(Alias = "theme")]
        private ApplicationTheme _theme;

        [SaveSettingsOnChange]
        partial void OnThemeChanged(ApplicationTheme value)
        {
            Log.Information($"Theme changed to {value}");
        }

        private void OnThemeMessage(object s, PropertyChangedMessage<ApplicationTheme> m)
        {
            switch (m.PropertyName)
            {
                case "CurrentTheme":
                    Theme = m.NewValue;
                    break;
            }
        }
    }
}
