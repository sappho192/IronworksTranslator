using IronworksTranslator.Models;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Data;

namespace IronworksTranslator.Helpers
{
    internal class DescriptionStringToEnumConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            //if (parameter is not string enumString)
            //{
            //    throw new ArgumentException("DescriptionStringToEnumConverterParameterMustBeAnEnumName");
            //}

            if (!Enum.IsDefined(typeof(ClientLanguage), value))
            {
                throw new ArgumentException("DescriptionStringToEnumConverterValueMustBeAnEnum");
            }

            var language = (ClientLanguage)value;
            var enumString = language.ToString();

            return enumString;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            //if (parameter is not string enumString)
            //{
            //    throw new ArgumentException("DescriptionStringToEnumConverterParameterMustBeAnEnumName");
            //}

            return EnumExtension.GetValueFromDescription<ClientLanguage>(value.ToString());
        }
    }
}
