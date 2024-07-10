using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using IronworksTranslator.Models.Enums;
using Serilog;
using YamlDotNet.Serialization;

namespace IronworksTranslator.Models.Settings
{
    public partial class TranslatorSettings : ObservableRecipient
    {
        public TranslatorSettings()
        {
            Messenger.Register<PropertyChangedMessage<ClientLanguage>>(this, OnClientLanguageMessage);
            Messenger.Register<PropertyChangedMessage<TranslatorEngine>>(this, OnTranslatorEngineMessage);
            Messenger.Register<PropertyChangedMessage<DialogueTranslationMethod>>(this, OnDialogueTranslationMethodMessage);
        }

        [ObservableProperty]
        [property: YamlMember(Alias = "client_language")]
        private ClientLanguage _clientLanguage;

        [ObservableProperty]
        [property: YamlMember(Alias = "translator_engine")]
        private TranslatorEngine _translatorEngine;

        [ObservableProperty]
        [property: YamlMember(Alias = "dialogue_translation_method")]
        private DialogueTranslationMethod _dialogueTranslationMethod;

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

        partial void OnTranslatorEngineChanged(TranslatorEngine value)
        {
            Log.Information($"TranslatorEngine changed to {value}");
            if (IronworksSettings.Instance != null)
            {
                IronworksSettings.UpdateSettingsFile(IronworksSettings.Instance);
            }
        }

        private void OnTranslatorEngineMessage(object s, PropertyChangedMessage<TranslatorEngine> m)
        {
            switch (m.PropertyName)
            {
                case "TranslatorEngine":
                    TranslatorEngine = m.NewValue;
                    break;
            }
        }

        partial void OnDialogueTranslationMethodChanged(DialogueTranslationMethod value)
        {
            Log.Information($"DialogueTranslationMethod changed to {value}");
            if (IronworksSettings.Instance != null)
            {
                IronworksSettings.UpdateSettingsFile(IronworksSettings.Instance);
            }
        }

        private void OnDialogueTranslationMethodMessage(object s, PropertyChangedMessage<DialogueTranslationMethod> m)
        {
            switch (m.PropertyName)
            {
                case "DialogueTranslationMethod":
                    DialogueTranslationMethod = m.NewValue;
                    break;
            }
        }
    }
}
