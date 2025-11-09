using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace WhisperFTPApp.Converters;

internal sealed class FtpStatusConverter : IValueConverter
{
    public static FtpStatusConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool hasOpenFtp)
        {
            return hasOpenFtp
                ? new SolidColorBrush(Colors.Blue)
                : new SolidColorBrush(Colors.Red);
        }

        return new SolidColorBrush(Colors.Gray);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
