using IronworksTranslator.Models;
using System.Globalization;
using System.Windows.Data;

namespace IronworksTranslator.Helpers
{
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
}
