using System.Globalization;
using Avalonia.Data.Converters;
using WhisperFTPApp.Enums;

namespace WhisperFTPApp.Converters;

internal sealed class FtpRowStyleClassConverter : IValueConverter
{
    public static readonly FtpRowStyleClassConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ServiceType serviceType && serviceType == ServiceType.FTP)
            return "ftpEnabled";
        return "ftpDisabled";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
