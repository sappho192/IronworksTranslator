using IronworksTranslator.Settings;

namespace IronworksTranslator.Core
{
    public class IronworksSettings
    {
        public static IronworksSettings Instance { get; set; }

        public IronworksSettings()
        {
            UI = new UISettings();
            Translator = new TranslatorSettings();
            Chat = new ChatSettings();
        }

        public readonly UISettings UI;
        public readonly TranslatorSettings Translator;
        public readonly ChatSettings Chat;
    }
}
