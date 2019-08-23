using IronworksTranslator.Settings;
using Newtonsoft.Json;
using System.IO;

namespace IronworksTranslator.Core
{
    public class IronworksSettings
    {
        public static IronworksSettings Instance { get; set; }

        public IronworksSettings()
        {
            UI = new UISettings();
            UI.OnSettingsChanged += UI_OnSettingsChanged;

            Translator = new TranslatorSettings();

            Chat = new ChatSettings();
        }

        private void UI_OnSettingsChanged(object sender, object changedProperty)
        {
            if (Instance != null)
            {
                Instance.UpdateSettingsFile();
            }
        }

        public void UpdateSettingsFile()
        {
            string settings = JsonConvert.SerializeObject(Instance, Formatting.Indented);
            File.WriteAllText("./settings/settings.json", settings);
        }

        public readonly UISettings UI;
        public readonly TranslatorSettings Translator;
        public readonly ChatSettings Chat;
    }
}
