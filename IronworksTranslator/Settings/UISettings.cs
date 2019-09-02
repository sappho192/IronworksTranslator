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
            ChatBackgroundOpacity = 0.75;

            DialogueWindowWidth = 500;
            DialogueWindowHeight = 80;
            DialogueBackgroundOpacity = 0.75;
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
        public double ChatBackgroundOpacity
        {
            get => chatBackgroundOpacity;
            set
            {
                if (value != chatBackgroundOpacity)
                {
                    chatBackgroundOpacity = value;
                    OnSettingsChanged?.Invoke(this, nameof(chatBackgroundOpacity), chatBackgroundOpacity);
                }
            }
        }
        private double chatBackgroundOpacity;

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

        /* Dialogue UI settings  */
        [JsonProperty]
        public double DialogueWindowWidth
        {
            get => dialogueWindowWidth;
            set
            {
                if (value != dialogueWindowWidth)
                {
                    dialogueWindowWidth = value;
                    OnSettingsChanged?.Invoke(this, nameof(dialogueWindowWidth), dialogueWindowWidth);
                }
            }
        }
        private double dialogueWindowWidth;

        [JsonProperty]
        public double DialogueWindowHeight
        {
            get => dialogueWindowHeight;
            set
            {
                if (value != dialogueWindowHeight)
                {
                    dialogueWindowHeight = value;
                    OnSettingsChanged?.Invoke(this, nameof(dialogueWindowHeight), dialogueWindowHeight);
                }
            }
        }
        private double dialogueWindowHeight;

        [JsonProperty]
        public double DialogueBackgroundOpacity
        {
            get => dialogueBackgroundOpacity;
            set
            {
                if (value != dialogueBackgroundOpacity)
                {
                    dialogueBackgroundOpacity = value;
                    OnSettingsChanged?.Invoke(this, nameof(dialogueBackgroundOpacity), dialogueBackgroundOpacity);
                }
            }
        }
        private double dialogueBackgroundOpacity;

        public event SettingsChangedEventHandler OnSettingsChanged;
    }
}
