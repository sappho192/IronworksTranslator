using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using Serilog;
using YamlDotNet.Serialization;

namespace IronworksTranslator.Models.Settings
{
    public partial class TranslatorSettings : ObservableRecipient
    {
        public TranslatorSettings()
        {
            Messenger.Register<PropertyChangedMessage<ClientLanguage>>(this, OnClientLanguageMessage);
        }

        [ObservableProperty]
        [property: YamlMember(Alias = "client_language")]
        private ClientLanguage _clientLanguage;

        partial void OnClientLanguageChanged(ClientLanguage value)
        {
            Log.Information($"ClientLanguage changed to {value}");
            if (IronworksSettings.Instance != null)
            {
                IronworksSettings.UpdateSettingsFile(IronworksSettings.Instance);
            }
        }

        private void OnClientLanguageMessage(object s, PropertyChangedMessage<ClientLanguage> m)
        {
            switch (m.PropertyName)
            {
                case "ClientLanguage":
                    ClientLanguage = m.NewValue;
                    break;
            }
        }
    }
}
