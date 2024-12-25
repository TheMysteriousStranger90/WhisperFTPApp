using System.Collections.Generic;

namespace WhisperFTPApp.Settings;

public class BackgroundSettings
{
    public string SelectedBackground { get; set; } = "avares://WhisperFTPApp/Assets/Image (3).jpg";
    public List<string> AvailableBackgrounds { get; } = new()
    {
        "avares://WhisperFTPApp/Assets/Image (1).jpg",
        "avares://WhisperFTPApp/Assets/Image (2).jpg", 
        "avares://WhisperFTPApp/Assets/Image (3).jpg",
        "avares://WhisperFTPApp/Assets/Image (4).jpg"
    };
}