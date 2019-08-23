namespace IronworksTranslator.Settings
{
    public delegate void SettingsChangedEventHandler(object sender, object changedProperty);
    public interface SettingsChangedEvent
    {
        event SettingsChangedEventHandler OnSettingsChanged;
    }
}
