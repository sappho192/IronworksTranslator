using System.Globalization;
using System.Windows.Data;
using IronworksTranslator.Helpers.Extensions;
using IronworksTranslator.Models.Enums;

namespace IronworksTranslator.Helpers.Converters
{
#pragma warning disable CS8604
    internal class LocalModelDevicePriorityToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is LocalModelDevicePriority priority ? EnumExtension.GetDescription(priority) : string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return EnumExtension.GetValueFromDescription<LocalModelDevicePriority>(value.ToString());
        }
    }
#pragma warning restore CS8604
}
