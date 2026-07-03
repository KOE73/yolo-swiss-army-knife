using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using YoloHelperApp.ViewModels;

namespace YoloHelperApp.Converters;

public class MetricToggleToBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is MetricToggle toggle)
        {
            bool isForeground = parameter?.ToString() == "foreground";

            if (toggle.IsEnabled)
            {
                if (isForeground)
                {
                    return Brushes.White;
                }
                else
                {
                    try
                    {
                        return new SolidColorBrush(Color.Parse(toggle.Color));
                    }
                    catch
                    {
                        return Brushes.Gray;
                    }
                }
            }
            else
            {
                if (isForeground)
                {
                    // Dark gray text when disabled
                    return new SolidColorBrush(Color.Parse("#475569"));
                }
                else
                {
                    // Light gray background when disabled
                    return new SolidColorBrush(Color.Parse("#F1F5F9"));
                }
            }
        }
        return Brushes.Gray;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
