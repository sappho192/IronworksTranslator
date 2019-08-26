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
                    OnSettingsChanged?.Invoke(this, nameof(ChatTextboxFontSize), ChatTextboxFontSize);
                }
            }
        } //px
        private int chatTextboxFontSize;

        [JsonProperty]
        public string ChatTextboxFontFamily
        {
            get => chatTextboxFontFamily;
            set
            {
                if (value != chatTextboxFontFamily)
                {
                    chatTextboxFontFamily = value;
                    OnSettingsChanged?.Invoke(this, nameof(chatTextboxFontFamily), chatTextboxFontFamily);
                }
            }
        }
        private string chatTextboxFontFamily;

        public event SettingsChangedEventHandler OnSettingsChanged;
    }
}
