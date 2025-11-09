namespace WhisperFTPApp.Settings;

public class BackgroundSettings
{
    public string SelectedBackground { get; set; } = "avares://WhisperFTPApp/Assets/Image (3).jpg";

    public IReadOnlyList<string> AvailableBackgrounds { get; } = new List<string>
    {
        "avares://WhisperFTPApp/Assets/Image (1).jpg",
        "avares://WhisperFTPApp/Assets/Image (2).jpg",
        "avares://WhisperFTPApp/Assets/Image (3).jpg",
        "avares://WhisperFTPApp/Assets/Image (4).jpg"
    };
}
