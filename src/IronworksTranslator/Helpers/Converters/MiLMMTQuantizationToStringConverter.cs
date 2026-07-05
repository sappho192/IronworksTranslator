using System.Globalization;
using System.Windows.Data;
using IronworksTranslator.Helpers.Extensions;
using IronworksTranslator.Models.Enums;

namespace IronworksTranslator.Helpers.Converters
{
#pragma warning disable CS8604
    internal class MiLMMTQuantizationToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is MiLMMTQuantization quantization ? EnumExtension.GetDescription(quantization) : string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return EnumExtension.GetValueFromDescription<MiLMMTQuantization>(value.ToString());
        }
    }
#pragma warning restore CS8604
}
