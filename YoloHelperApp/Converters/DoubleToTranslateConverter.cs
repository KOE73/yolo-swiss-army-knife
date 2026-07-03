using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace YoloHelperApp.Converters;

public class DoubleToTranslateConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double d)
        {
            // Scale translation to pixels for visual shift on canvas (e.g. 0.0-0.5 maps to 0-50 pixels)
            return d * 100.0;
        }
        if (value is float f)
        {
            return (double)f * 100.0;
        }
        return 0.0;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
