using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using IronworksTranslator.Models.Enums;
using Serilog;
using YamlDotNet.Serialization;
using IronworksTranslator.Utils;
using IronworksTranslator.ViewModels.Pages;
using ObservableCollections;
using IronworksTranslator.Utils.Aspect;

namespace IronworksTranslator.Models.Settings
{
    public partial class TranslatorSettings : ObservableRecipient
    {
        public TranslatorSettings()
        {
            Messenger.Register<PropertyChangedMessage<ClientLanguage>>(this, OnClientLanguageMessage);
            Messenger.Register<PropertyChangedMessage<TranslatorEngine>>(this, OnTranslatorEngineMessage);
            Messenger.Register<PropertyChangedMessage<MiLMMTModelSize>>(this, OnMiLMMTModelSizeMessage);
            Messenger.Register<PropertyChangedMessage<MiLMMTQuantization>>(this, OnMiLMMTQuantizationMessage);
            Messenger.Register<PropertyChangedMessage<LocalModelDevicePriority>>(this, OnLocalModelDevicePriorityMessage);
            Messenger.Register<PropertyChangedMessage<DialogueTranslationMethod>>(this, OnDialogueTranslationMethodMessage);
            Messenger.Register<PropertyChangedMessage<bool>>(this, OnBoolMessage);
        }

        [TraceMethod]
        public void InitializeCollectionListeners()
        {
            if (DeeplApiKeys == null)
            {
                Log.Fatal("DeeplApiKeys is null");
                MessageBox.Show(Localizer.GetString("app.exception.description"));
                return;
            }
            DeeplApiKeys.CollectionChanged += DeeplApiKeys_CollectionChanged;
        }

        private void DeeplApiKeys_CollectionChanged(in NotifyCollectionChangedEventArgs<string> e)
        {
            if (IronworksSettings.Instance != null)
            {
                IronworksSettings.UpdateSettingsFile(IronworksSettings.Instance);
            }
        }

        [ObservableProperty]
        [property: YamlMember(Alias = "client_language")]
        private ClientLanguage _clientLanguage;

        [ObservableProperty]
        [property: YamlMember(Alias = "translator_engine")]
        private TranslatorEngine _translatorEngine;

        [ObservableProperty]
        [property: YamlMember(Alias = "milmmt_model_size")]
        private MiLMMTModelSize _miLMMTModelSize;

        [ObservableProperty]
        [property: YamlMember(Alias = "milmmt_quantization")]
        private MiLMMTQuantization _miLMMTQuantization;

        [ObservableProperty]
        [property: YamlMember(Alias = "local_model_device_priority")]
        private LocalModelDevicePriority _localModelDevicePriority;

        [ObservableProperty]
        [property: YamlMember(Alias = "local_model_device_priority_user_selected")]
        private bool _localModelDevicePriorityUserSelected;

        [ObservableProperty]
        [property: YamlMember(Alias = "dialogue_translation_method")]
        private DialogueTranslationMethod _dialogueTranslationMethod;

        [ObservableProperty]
        [property: YamlMember(Alias = "deepl_api_keys")]
        private ObservableList<string>? _deeplApiKeys;

        [ObservableProperty]
        [property: YamlMember(Alias = "deepl_auto_source_language")]
        private bool _deeplAutoSourceLanguage;

        [ObservableProperty]
        [property: YamlMember(Alias = "use_internal_address")]
        private bool _useInternalAddress;

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
        partial void OnMiLMMTModelSizeChanged(MiLMMTModelSize value)
        {
            Log.Information("MiLLMTModelSize changed to {Value}", value);
        }

        private void OnMiLMMTModelSizeMessage(object s, PropertyChangedMessage<MiLMMTModelSize> m)
        {
            switch (m.PropertyName)
            {
                case nameof(SettingsViewModel.MiLMMTModelSize):
                    MiLMMTModelSize = m.NewValue;
                    break;
            }
        }

        [SaveSettingsOnChange]
        partial void OnMiLMMTQuantizationChanged(MiLMMTQuantization value)
        {
            Log.Information("MiLLMTQuantization changed to {Value}", value);
        }

        private void OnMiLMMTQuantizationMessage(object s, PropertyChangedMessage<MiLMMTQuantization> m)
        {
            switch (m.PropertyName)
            {
                case nameof(SettingsViewModel.MiLMMTQuantization):
                    MiLMMTQuantization = m.NewValue;
                    break;
            }
        }

        [SaveSettingsOnChange]
        partial void OnLocalModelDevicePriorityChanged(LocalModelDevicePriority value)
        {
            Log.Information("LocalModelDevicePriority changed to {Value}", value);
        }

        private void OnLocalModelDevicePriorityMessage(object s, PropertyChangedMessage<LocalModelDevicePriority> m)
        {
            switch (m.PropertyName)
            {
                case nameof(SettingsViewModel.LocalModelDevicePriority):
                    LocalModelDevicePriorityUserSelected = true;
                    LocalModelDevicePriority = m.NewValue;
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
        partial void OnDeeplAutoSourceLanguageChanged(bool value)
        {
            Log.Information($"DeeplAutoSourceLanguage changed to {value}");
        }

        private void OnBoolMessage(object recipient, PropertyChangedMessage<bool> message)
        {
            switch (message.PropertyName)
            {
                case nameof(SettingsViewModel.DeeplAutoSourceLanguage):
                    DeeplAutoSourceLanguage = message.NewValue;
                    break;
            }
        }
    }
}
