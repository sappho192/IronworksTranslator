using Newtonsoft.Json;

namespace IronworksTranslator.Settings
{
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class UISettings : SettingsChangedEvent
    {
        public UISettings()
        {
            ChatTextboxFontSize = 12;
            MainWindowWidth = 420;
            MainWindowHeight = 200;
        }

        /* General UI settings */
        [JsonProperty]
        public double MainWindowWidth
        {
            get => mainWindowWidth;
            set
            {
                if (value != mainWindowWidth)
                {
                    mainWindowWidth = value;
                    OnSettingsChanged?.Invoke(this, nameof(mainWindowWidth), mainWindowWidth);
                }
            }
        }
        private double mainWindowWidth;
        [JsonProperty]
        public double MainWindowHeight
        {
            get => mainWindowHeight;
            set
            {
                if (value != mainWindowHeight)
                {
                    mainWindowHeight = value;
                    OnSettingsChanged?.Invoke(this, nameof(mainWindowHeight), mainWindowHeight);
                }
            }
        }
        private double mainWindowHeight;

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
