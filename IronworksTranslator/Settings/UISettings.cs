using Newtonsoft.Json;

namespace IronworksTranslator.Settings
{
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class UISettings : SettingsChangedEvent
    {
        public UISettings()
        {
            ChatTextboxFontSize = 12;
        }

        /* General UI settings */


        /* Chat UI settings  */
        [JsonProperty]
        public int ChatTextboxFontSize
        {
            get => chatTextboxFontSize;
            set
            {
                if (value != chatTextboxFontSize)
                {
                    chatTextboxFontSize = value;
                    if (OnSettingsChanged != null)
                    {
                        OnSettingsChanged(this, ChatTextboxFontSize);
                    }
                    //NotifyPropertyChanged();
                }
            }
        } //px
        private int chatTextboxFontSize;

        public event SettingsChangedEventHandler OnSettingsChanged;
    }
}
