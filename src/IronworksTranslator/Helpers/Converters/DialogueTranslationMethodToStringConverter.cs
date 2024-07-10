using IronworksTranslator.Helpers.Extensions;
using IronworksTranslator.Models.Enums;
using System.Globalization;
using System.Windows.Data;

namespace IronworksTranslator.Helpers.Converters
{
    internal class DialogueTranslationMethodToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!Enum.IsDefined(typeof(DialogueTranslationMethod), value))
            {
                throw new ArgumentException("DialogueTranslationMethodToStringConverterValueMustBeAnEnum");
            }

            var language = (DialogueTranslationMethod)value;
            var enumString = language.ToString();

            return enumString;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return EnumExtension.GetValueFromDescription<DialogueTranslationMethod>(value.ToString());
        }
    }
}
