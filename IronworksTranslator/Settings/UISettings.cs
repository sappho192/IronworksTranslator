using Newtonsoft.Json;

namespace IronworksTranslator.Settings
{
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class UISettings
    {
        public UISettings()
        {
            ChatTextboxFontSize = 12;
        }

        /* General UI settings */


        /* Chat UI settings  */
        [JsonProperty]
        public int ChatTextboxFontSize { get; set; } //px
    }
}
