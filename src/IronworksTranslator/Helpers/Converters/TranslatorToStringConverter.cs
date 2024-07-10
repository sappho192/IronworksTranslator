using System.Globalization;
using System.Windows.Data;
using IronworksTranslator.Models;

namespace IronworksTranslator.Helpers
{
    internal class TranslatorToStringConverter : IValueConverter
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
