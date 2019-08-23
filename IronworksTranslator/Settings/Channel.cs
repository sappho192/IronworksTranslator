using IronworksTranslator.Core;

namespace IronworksTranslator.Settings
{
    public class Channel
    {
        public Channel()
        {
            Show = true;
            MajorLanguage = ClientLanguage.Japanese;
        }

        bool Show { get; set; }
        ClientLanguage MajorLanguage { get; set; }
    }
}
