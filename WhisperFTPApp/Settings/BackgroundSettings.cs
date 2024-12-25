using System.Collections.Generic;

namespace WhisperFTPApp.Settings;

public class BackgroundSettings
{
    public string SelectedBackground { get; set; } = "/Assets/Image (3).jpg";
    public List<string> AvailableBackgrounds { get; } = new()
    {
        "/Assets/Image (1).jpg",
        "/Assets/Image (2).jpg", 
        "/Assets/Image (3).jpg",
        "/Assets/Image (4).jpg"
    };
}