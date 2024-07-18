using System.Globalization;
using System.Windows.Data;
using Wpf.Ui.Controls;

namespace IronworksTranslator.Helpers.Converters
{
    internal class BooleanToEyeSymbolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            SymbolRegular symbol = (bool)value ? SymbolRegular.Eye24 : SymbolRegular.EyeOff24;
            return symbol;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool showOrNot = (SymbolRegular)value == SymbolRegular.Eye24;
            return showOrNot;
        }
    }
}
