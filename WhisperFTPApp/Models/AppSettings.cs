using WhisperFTPApp.Constants;

namespace WhisperFTPApp.Models;

public class AppSettings
{
    public string BackgroundPathImage { get; set; } = "/Assets/Image (3).jpg";
    public string Language { get; set; } = AppConstants.DefaultLanguage;
}
