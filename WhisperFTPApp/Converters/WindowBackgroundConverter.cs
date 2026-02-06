using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace WhisperFTPApp.Converters;

internal sealed class WindowBackgroundConverter : IValueConverter
{
    public static readonly WindowBackgroundConverter Instance = new();

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope",
        Justification = "ImageBrush takes ownership of the Bitmap and will dispose it")]
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string path)
        {
            try
            {
                if (path.StartsWith("avares://", StringComparison.Ordinal))
                {
                    var uri = new Uri(path);
                    var stream = AssetLoader.Open(uri);
                    var bitmap = new Bitmap(stream);
                    return new ImageBrush(bitmap)
                    {
                        Stretch = Stretch.UniformToFill
                    };
                }

                var fileBitmap = new Bitmap(path);
                return new ImageBrush(fileBitmap)
                {
                    Stretch = Stretch.UniformToFill
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading background image: {ex.Message}");
                return null;
            }
        }

        return null;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope",
        Justification = "ImageBrush takes ownership of the Bitmap and will dispose it")]
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
