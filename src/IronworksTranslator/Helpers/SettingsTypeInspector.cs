using YamlDotNet.Serialization.TypeInspectors;
using YamlDotNet.Serialization;

namespace IronworksTranslator.Helpers
{
    public class SettingsTypeInspector : TypeInspectorSkeleton
    {
        private readonly ITypeInspector _innerTypeDescriptor;

        public SettingsTypeInspector(ITypeInspector innerTypeDescriptor)
        {
            _innerTypeDescriptor = innerTypeDescriptor;
        }

        public override IEnumerable<IPropertyDescriptor> GetProperties(Type type, object? container)
        {
            var props = _innerTypeDescriptor.GetProperties(type, container);
            props = props.Where(p => !(p.Type == typeof(Dictionary<string, object>) && (p.Name == "ui_settings" || p.Name == "channel_settings")));
            props = props.Where(p => p.Name != "is_active"); // Ignore the Property which is from the base class, ObservableRecipient
            return props;
        }
    }
}
