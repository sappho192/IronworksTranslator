using IronworksTranslator.Settings;
using Newtonsoft.Json;
using Serilog;
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
            Translator.OnSettingsChanged += Translator_OnSettingsChanged;

            Chat = new ChatSettings();
        }

        private void Translator_OnSettingsChanged(object sender, string name, object value)
        {
            onSettingsChanged("Translator", sender, name, value);
        }

        private void UI_OnSettingsChanged(object sender, string name, object value)
        {
            onSettingsChanged("UI", sender, name, value);
        }

        private void onSettingsChanged(string group, object sender, string name, object value)
        {
            string template = " settings {@propertyName} changed to {@value}";

            Log.Debug($"{group}{template}", name, value);
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
