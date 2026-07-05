using System.Globalization;
using System.Windows.Data;
using IronworksTranslator.Helpers.Extensions;
using IronworksTranslator.Models.Enums;

namespace IronworksTranslator.Helpers.Converters
{
#pragma warning disable CS8604
    internal class MiLMMTModelSizeToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is MiLMMTModelSize modelSize ? EnumExtension.GetDescription(modelSize) : string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return EnumExtension.GetValueFromDescription<MiLMMTModelSize>(value.ToString());
        }
    }
#pragma warning restore CS8604
}
