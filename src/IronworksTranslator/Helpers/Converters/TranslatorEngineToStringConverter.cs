using System.Globalization;
using System.Windows.Data;
using IronworksTranslator.Helpers.Extensions;
using IronworksTranslator.Models.Enums;

namespace IronworksTranslator.Helpers.Converters
{
#pragma warning disable CS8604
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
#pragma warning restore CS8604
}
