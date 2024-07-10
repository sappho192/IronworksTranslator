using System.Globalization;
using System.Windows.Data;
using IronworksTranslator.Helpers.Extensions;
using IronworksTranslator.Models.Enums;

namespace IronworksTranslator.Helpers.Converters
{
    internal class TranslatorEngineToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!Enum.IsDefined(typeof(TranslatorEngine), value))
            {
                throw new ArgumentException("TranslatorEngineToStringConverterValueMustBeAnEnum");
            }

            var language = (TranslatorEngine)value;
            var enumString = language.ToString();

            return enumString;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return EnumExtension.GetValueFromDescription<TranslatorEngine>(value.ToString());
        }
    }
}
