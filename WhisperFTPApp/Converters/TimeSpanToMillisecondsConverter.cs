using System.Globalization;
using Avalonia.Data.Converters;

namespace WhisperFTPApp.Converters;

public class TimeSpanToMillisecondsConverter : IValueConverter
{
    public static readonly TimeSpanToMillisecondsConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is TimeSpan timeSpan)
        {
            return $"{timeSpan.TotalMilliseconds:F0}ms";
        }

        return "N/A";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
