using IronworksTranslator.Core;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace IronworksTranslator.Settings
{
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class TranslatorSettings : SettingsChangedEvent
    {
        public TranslatorSettings()
        {
            DefaultTranslatorEngine = TranslatorEngine.Papago;
            ActiveTranslatorEngines = new HashSet<TranslatorEngine>
            {
                TranslatorEngine.Papago
            };
            NativeLanguage = ClientLanguage.Korean;
        }


        [JsonProperty]
        public TranslatorEngine DefaultTranslatorEngine
        {
            get => defaultTranslatorEngine;
            set
            {
                if (value != defaultTranslatorEngine)
                {
                    defaultTranslatorEngine = value;
                    OnSettingsChanged?.Invoke(this, nameof(defaultTranslatorEngine), defaultTranslatorEngine);
                }
            }
        }
        [JsonProperty]
        public HashSet<TranslatorEngine> ActiveTranslatorEngines { get; } // How to attach event?
        [JsonProperty]
        public ClientLanguage NativeLanguage
        {
            get => nativeLanguage;
            set
            {
                if (value != nativeLanguage)
                {
                    nativeLanguage = value;
                    OnSettingsChanged?.Invoke(this, nameof(nativeLanguage), nativeLanguage);
                }
            }
        }

        private TranslatorEngine defaultTranslatorEngine;
        private ClientLanguage nativeLanguage;

        public event SettingsChangedEventHandler OnSettingsChanged;
    }
}
