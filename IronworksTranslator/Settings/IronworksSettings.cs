using IronworksTranslator.Settings;

namespace IronworksTranslator.Core
{
    public class IronworksSettings
    {
        private static IronworksSettings _instance;

        public static IronworksSettings Instance()
        {// make new instance if null
            return _instance ?? (_instance = new IronworksSettings());
        }
        protected IronworksSettings()
        {
            UI = new UISettings();
            Translator = new TranslatorSettings();
            Chat = new ChatSettings();
        }

        public UISettings UI;
        public TranslatorSettings Translator;
        public ChatSettings Chat;
    }
}
