﻿using IronworksTranslator.Core;
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
            DialogueLanguage = ClientLanguage.Japanese;
            DefaultDialogueTranslationMethod = 0; // Memory Search
            UseInternalAddress = false;
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
        public DialogueTranslationMethod DefaultDialogueTranslationMethod
        {
            get => dialogueTranslationMethod;
            set
            {
                if (value != dialogueTranslationMethod)
                {
                    dialogueTranslationMethod = value;
                    OnSettingsChanged?.Invoke(this, nameof(dialogueTranslationMethod), dialogueTranslationMethod);
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
        [JsonProperty]
        public ClientLanguage DialogueLanguage
        {
            get => dialogueLanguage;
            set
            {
                if (value != dialogueLanguage)
                {
                    dialogueLanguage = value;
                    OnSettingsChanged?.Invoke(this, nameof(DialogueLanguage), DialogueLanguage);
                }
            }
        }

        [JsonProperty]
        public bool UseInternalAddress
        {
            get => useInternalAddress;
            set
            {
                if (value != useInternalAddress)
                {
                    useInternalAddress = value;
                    OnSettingsChanged?.Invoke(this, nameof(UseInternalAddress), UseInternalAddress);
                }
            }
        }

        private ClientLanguage dialogueLanguage;

        private TranslatorEngine defaultTranslatorEngine;
        private ClientLanguage nativeLanguage;

        private DialogueTranslationMethod dialogueTranslationMethod;

        private bool useInternalAddress;

        public event SettingsChangedEventHandler OnSettingsChanged;
    }
}
