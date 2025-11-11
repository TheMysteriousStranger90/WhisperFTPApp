using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace WhisperFTPApp.Converters;

public sealed class WindowBackgroundConverter : IValueConverter
{
    public static readonly WindowBackgroundConverter Instance = new();

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
                    return new ImageBrush(new Bitmap(stream))
                    {
                        Stretch = Stretch.UniformToFill
                    };
                }

                return new ImageBrush(new Bitmap(path))
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

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
