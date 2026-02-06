using WhisperFTPApp.Constants;

namespace WhisperFTPApp.Settings;

public class BackgroundSettings
{
    public string SelectedBackground { get; set; } = AppConstants.DefaultBackground;

    public IReadOnlyList<string> AvailableBackgrounds { get; } = new List<string>
    {
        $"{AppConstants.AvaresPrefix}/Assets/Image (1).jpg",
        $"{AppConstants.AvaresPrefix}/Assets/Image (2).jpg",
        $"{AppConstants.AvaresPrefix}/Assets/Image (3).jpg",
        $"{AppConstants.AvaresPrefix}/Assets/Image (4).jpg"
    };
}
