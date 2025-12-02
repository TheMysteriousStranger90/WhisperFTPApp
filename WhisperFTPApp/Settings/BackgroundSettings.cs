namespace WhisperFTPApp.Settings;

public class BackgroundSettings
{
    public string SelectedBackground { get; set; } = "avares://AzioWhisperFTP/Assets/Image (3).jpg";

    public IReadOnlyList<string> AvailableBackgrounds { get; } = new List<string>
    {
        "avares://AzioWhisperFTP/Assets/Image (1).jpg",
        "avares://AzioWhisperFTP/Assets/Image (2).jpg",
        "avares://AzioWhisperFTP/Assets/Image (3).jpg",
        "avares://AzioWhisperFTP/Assets/Image (4).jpg"
    };
}
