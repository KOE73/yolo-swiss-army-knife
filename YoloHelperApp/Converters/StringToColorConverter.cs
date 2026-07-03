using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace YoloHelperApp.Converters;

public class StringToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string hex && !string.IsNullOrWhiteSpace(hex))
        {
            try
            {
                return Color.Parse(hex);
            }
            catch
            {
                // Fallback to blue
            }
        }
        return Colors.Blue;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is Color color)
        {
            return color.ToString(); // Returns #AARRGGBB or similar hex string
        }
        return "#3B82F6";
    }
}
