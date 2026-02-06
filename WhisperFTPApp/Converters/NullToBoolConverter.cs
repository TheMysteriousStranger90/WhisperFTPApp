using System.Globalization;
using Avalonia.Data.Converters;

namespace WhisperFTPApp.Converters;

internal sealed class NullToBoolConverter : IValueConverter
{
    public static NullToBoolConverter Instance { get; } = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var invert = parameter?.ToString()?.Equals("Invert", StringComparison.OrdinalIgnoreCase) == true;
        var isNull = value == null;

        return invert ? isNull : !isNull;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
