using System.Globalization;
using Avalonia.Data.Converters;
using WhisperFTPApp.Helpers;

namespace WhisperFTPApp.Converters;

internal sealed class FileSizeConverter : IValueConverter
{
    public static FileSizeConverter Instance { get; } = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is long size)
        {
            return FileHelper.FormatFileSize(size);
        }

        return "0 B";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
