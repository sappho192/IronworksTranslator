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
            UI.OnSettingsChanged += (sender, name, value) => { onSettingsChanged("UI", sender, name, value); };

            Translator = new TranslatorSettings();
            Translator.OnSettingsChanged += (sender, name, value) => { onSettingsChanged("Translator", sender, name, value); };

            Chat = new ChatSettings();
            Chat.Emote.OnSettingsChanged += (sender, name, value) => { onSettingsChanged("Emote@Chat", sender, name, value); };
            Chat.Tell.OnSettingsChanged += (sender, name, value) => { onSettingsChanged("Tell@Chat", sender, name, value); };
            Chat.Say.OnSettingsChanged += (sender, name, value) => { onSettingsChanged("Say@Chat", sender, name, value); };
            Chat.Yell.OnSettingsChanged += (sender, name, value) => { onSettingsChanged("Yell@Chat", sender, name, value); };
            Chat.Shout.OnSettingsChanged += (sender, name, value) => { onSettingsChanged("Shout@Chat", sender, name, value); };
            Chat.Party.OnSettingsChanged += (sender, name, value) => { onSettingsChanged("Party@Chat", sender, name, value); };
            Chat.Alliance.OnSettingsChanged += (sender, name, value) => { onSettingsChanged("Alliance@Chat", sender, name, value); };
            Chat.LinkShell1.OnSettingsChanged += (sender, name, value) => { onSettingsChanged("LinkShell1@Chat", sender, name, value); };
            Chat.LinkShell2.OnSettingsChanged += (sender, name, value) => { onSettingsChanged("LinkShell2@Chat", sender, name, value); };
            Chat.LinkShell3.OnSettingsChanged += (sender, name, value) => { onSettingsChanged("LinkShell3@Chat", sender, name, value); };
            Chat.LinkShell4.OnSettingsChanged += (sender, name, value) => { onSettingsChanged("LinkShell4@Chat", sender, name, value); };
            Chat.LinkShell5.OnSettingsChanged += (sender, name, value) => { onSettingsChanged("LinkShell5@Chat", sender, name, value); };
            Chat.LinkShell6.OnSettingsChanged += (sender, name, value) => { onSettingsChanged("LinkShell6@Chat", sender, name, value); };
            Chat.LinkShell7.OnSettingsChanged += (sender, name, value) => { onSettingsChanged("LinkShell7@Chat", sender, name, value); };
            Chat.LinkShell8.OnSettingsChanged += (sender, name, value) => { onSettingsChanged("LinkShell8@Chat", sender, name, value); };
            Chat.CWLinkShell1.OnSettingsChanged += (sender, name, value) => { onSettingsChanged("CWLinkShell1@Chat", sender, name, value); };
            Chat.CWLinkShell2.OnSettingsChanged += (sender, name, value) => { onSettingsChanged("CWLinkShell2@Chat", sender, name, value); };
            Chat.CWLinkShell3.OnSettingsChanged += (sender, name, value) => { onSettingsChanged("CWLinkShell3@Chat", sender, name, value); };
            Chat.CWLinkShell4.OnSettingsChanged += (sender, name, value) => { onSettingsChanged("CWLinkShell4@Chat", sender, name, value); };
            Chat.CWLinkShell5.OnSettingsChanged += (sender, name, value) => { onSettingsChanged("CWLinkShell5@Chat", sender, name, value); };
            Chat.CWLinkShell6.OnSettingsChanged += (sender, name, value) => { onSettingsChanged("CWLinkShell6@Chat", sender, name, value); };
            Chat.CWLinkShell7.OnSettingsChanged += (sender, name, value) => { onSettingsChanged("CWLinkShell7@Chat", sender, name, value); };
            Chat.CWLinkShell8.OnSettingsChanged += (sender, name, value) => { onSettingsChanged("CWLinkShell8@Chat", sender, name, value); };
            Chat.FreeCompany.OnSettingsChanged += (sender, name, value) => { onSettingsChanged("FreeCompany@Chat", sender, name, value); };
            Chat.Novice.OnSettingsChanged += (sender, name, value) => { onSettingsChanged("Novice@Chat", sender, name, value); };
            Chat.Echo.OnSettingsChanged += (sender, name, value) => { onSettingsChanged("Echo@Chat", sender, name, value); };
            Chat.Notice.OnSettingsChanged += (sender, name, value) => { onSettingsChanged("Notice@Chat", sender, name, value); };
            Chat.System.OnSettingsChanged += (sender, name, value) => { onSettingsChanged("System@Chat", sender, name, value); };
            Chat.Error.OnSettingsChanged += (sender, name, value) => { onSettingsChanged("Error@Chat", sender, name, value); };
            Chat.NPCDialog.OnSettingsChanged += (sender, name, value) => { onSettingsChanged("NPCDialog@Chat", sender, name, value); };
            Chat.NPCAnnounce.OnSettingsChanged += (sender, name, value) => { onSettingsChanged("NPCAnnounce@Chat", sender, name, value); };
            Chat.MarketSold.OnSettingsChanged += (sender, name, value) => { onSettingsChanged("MarketSold@Chat", sender, name, value); };
            Chat.Recruitment.OnSettingsChanged += (sender, name, value) => { onSettingsChanged("Recruitment@Chat", sender, name, value); };
        }

        private void onSettingsChanged(string group, object sender, string name, object value, bool showValue = true)
        {
            string template = showValue ? " settings {@propertyName} changed to {@value}"
                : " settings {@propertyName} changed";

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
        [JsonProperty]
        public const string SettingsVersion = "0.0.5";
    }
}
