using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using WhisperFTPApp.Enums;

namespace WhisperFTPApp.Converters;

public class ScanStatusColorConverter : IMultiValueConverter
{
    public static readonly ScanStatusColorConverter Instance = new();

    public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count > 0 && values[0] is FtpScanStatus status)
        {
            return status switch
            {
                FtpScanStatus.NotScanned => new SolidColorBrush(Color.FromRgb(158, 158, 158)),
                FtpScanStatus.Scanning => new SolidColorBrush(Color.FromRgb(33, 150, 243)),
                FtpScanStatus.Found => new SolidColorBrush(Color.FromRgb(76, 175, 80)),
                FtpScanStatus.NotFound => new SolidColorBrush(Color.FromRgb(255, 152, 0)),
                FtpScanStatus.Error => new SolidColorBrush(Color.FromRgb(244, 67, 54)),
                FtpScanStatus.RequiresConnection => new SolidColorBrush(Color.FromRgb(156, 39, 176)),
                _ => new SolidColorBrush(Color.FromRgb(158, 158, 158))
            };
        }

        return new SolidColorBrush(Color.FromRgb(158, 158, 158));
    }

#pragma warning disable S2325 // Methods and properties that don't access instance data should be static
    public object?[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException("ConvertBack is not supported for ScanStatusColorConverter");
    }
#pragma warning restore S2325
}
