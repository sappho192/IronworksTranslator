using IronworksTranslator.Helpers.Extensions;
using IronworksTranslator.Models.Enums;
using System.Globalization;
using System.Windows.Data;

namespace IronworksTranslator.Helpers.Converters
{
#pragma warning disable CS8604
    internal class ClientLanguageToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!Enum.IsDefined(typeof(ClientLanguage), value))
            {
                throw new ArgumentException("ClientLanguageToStringConverterValueMustBeAnEnum");
            }

            var language = (ClientLanguage)value;
            var enumString = language.ToString();

            return enumString;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return EnumExtension.GetValueFromDescription<ClientLanguage>(value.ToString());
        }
    }
#pragma warning restore CS8604
}
