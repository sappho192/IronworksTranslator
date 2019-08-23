using IronworksTranslator.Core;
using Newtonsoft.Json;

namespace IronworksTranslator.Settings
{
    [JsonObject(MemberSerialization.OptIn)]
    public class Channel
    {
        public Channel()
        {
            Show = true;
            MajorLanguage = ClientLanguage.Japanese;
        }

        [JsonProperty]
        bool Show { get; set; }
        [JsonProperty]
        ClientLanguage MajorLanguage { get; set; }
    }
}
