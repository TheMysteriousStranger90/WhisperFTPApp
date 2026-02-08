using System.Globalization;
using Avalonia.Data.Converters;

namespace WhisperFTPApp.Converters;

internal sealed class BoolToVisibilityConverter : IValueConverter
{
    public static BoolToVisibilityConverter Instance { get; } = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            var invert = parameter?.ToString()?.Equals("Invert", StringComparison.OrdinalIgnoreCase) == true;
            return invert ? !boolValue : boolValue;
        }

        return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return Convert(value, targetType, parameter, culture);
    }
}
