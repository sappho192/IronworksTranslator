using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace IronworksTranslator.Helpers.Converters
{
    /// <summary>
    /// Converts an opacity value (0.0-1.0) to a SolidColorBrush with that opacity applied to the color.
    /// This allows the background to be transparent while keeping text opaque.
    /// </summary>
    internal class OpacityToBackgroundBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not double opacity)
            {
                return new SolidColorBrush(Colors.Black);
            }

            // Clamp opacity between 0.0 and 1.0
            opacity = Math.Max(0.0, Math.Min(1.0, opacity));

            // Get base color from parameter, default to Black
            Color baseColor = Colors.Black;
            if (parameter is string colorString)
            {
                try
                {
                    baseColor = (Color)ColorConverter.ConvertFromString(colorString);
                }
                catch
                {
                    baseColor = Colors.Black;
                }
            }

            // Create color with opacity applied
            byte alpha = (byte)(opacity * 255);
            Color transparentColor = Color.FromArgb(alpha, baseColor.R, baseColor.G, baseColor.B);

            return new SolidColorBrush(transparentColor);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
