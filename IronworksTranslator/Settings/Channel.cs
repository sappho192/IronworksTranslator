﻿using IronworksTranslator.Core;
using Newtonsoft.Json;

namespace IronworksTranslator.Settings
{
    [JsonObject(MemberSerialization.OptIn)]
    public class Channel : SettingsChangedEvent
    {
        public Channel()
        {
            Show = true;
            MajorLanguage = ClientLanguage.Japanese;
        }

        [JsonProperty]
        public bool Show
        {
            get => show;
            set
            {
                if (value != show)
                {
                    show = value;
                    OnSettingsChanged?.Invoke(this, nameof(Show), Show);
                }
            }
        }
        private bool show;

        [JsonProperty]
        public ClientLanguage MajorLanguage
        {
            get => majorLanguage;
            set
            {
                if (value != majorLanguage)
                {
                    majorLanguage = value;
                    OnSettingsChanged?.Invoke(this, nameof(MajorLanguage), MajorLanguage);
                }
            }
        }
        private ClientLanguage majorLanguage;

        public event SettingsChangedEventHandler OnSettingsChanged;
    }
}
