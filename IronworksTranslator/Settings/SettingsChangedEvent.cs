namespace IronworksTranslator.Settings
{
    public delegate void SettingsChangedEventHandler(object sender, string name, object value);
    public interface SettingsChangedEvent
    {
        event SettingsChangedEventHandler OnSettingsChanged;
    }
}
