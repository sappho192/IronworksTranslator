using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using IronworksTranslator.Models.Enums;
using Serilog;
using YamlDotNet.Serialization;
using IronworksTranslator.Utils;
using IronworksTranslator.ViewModels.Pages;
using IronworksTranslator.Utils.Translator;

namespace IronworksTranslator.Models.Settings
{
    public partial class TranslatorSettings : ObservableRecipient
    {
        public TranslatorSettings()
        {
            Messenger.Register<PropertyChangedMessage<ClientLanguage>>(this, OnClientLanguageMessage);
            Messenger.Register<PropertyChangedMessage<TranslatorEngine>>(this, OnTranslatorEngineMessage);
            Messenger.Register<PropertyChangedMessage<DialogueTranslationMethod>>(this, OnDialogueTranslationMethodMessage);
            Messenger.Register<PropertyChangedMessage<string>>(this, OnDeeplApiKeyMessage);
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

        [ObservableProperty]
        [property: YamlMember(Alias = "deepl_api_key")]
        private string? _deeplApiKey;

        [SaveSettingsOnChange]
        partial void OnClientLanguageChanged(ClientLanguage value)
        {
            Log.Information($"ClientLanguage changed to {value}");
        }

        private void OnClientLanguageMessage(object s, PropertyChangedMessage<ClientLanguage> m)
        {
            switch (m.PropertyName)
            {
                case nameof(SettingsViewModel.ClientLanguage):
                    ClientLanguage = m.NewValue;
                    break;
            }
        }

        [SaveSettingsOnChange]
        partial void OnTranslatorEngineChanged(TranslatorEngine value)
        {
            Log.Information($"TranslatorEngine changed to {value}");
        }

        private void OnTranslatorEngineMessage(object s, PropertyChangedMessage<TranslatorEngine> m)
        {
            switch (m.PropertyName)
            {
                case nameof(SettingsViewModel.TranslatorEngine):
                    TranslatorEngine = m.NewValue;
                    break;
            }
        }

        [SaveSettingsOnChange]
        partial void OnDialogueTranslationMethodChanged(DialogueTranslationMethod value)
        {
            Log.Information($"DialogueTranslationMethod changed to {value}");
        }

        private void OnDialogueTranslationMethodMessage(object s, PropertyChangedMessage<DialogueTranslationMethod> m)
        {
            switch (m.PropertyName)
            {
                case nameof(SettingsViewModel.DialogueTranslationMethod):
                    DialogueTranslationMethod = m.NewValue;
                    break;
            }
        }

        [SaveSettingsOnChange]
        partial void OnDeeplApiKeyChanged(string? value)
        {
            Log.Information($"DeeplApiKey changed to {value}");
        }

        private void OnDeeplApiKeyMessage(object recipient, PropertyChangedMessage<string> message)
        {
            switch (message.PropertyName)
            {
                case nameof(SettingsViewModel.DeeplApiKey):
                    DeeplApiKey = message.NewValue;
                    App.GetService<DeepLAPITranslator>().InitTranslator(testApi: true);
                    break;
            }
        }

    }
}
