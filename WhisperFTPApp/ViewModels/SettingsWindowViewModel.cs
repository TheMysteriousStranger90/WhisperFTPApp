using WhisperFTPApp.Services.Interfaces;

namespace WhisperFTPApp.ViewModels;

public class SettingsWindowViewModel : ViewModelBase
{
    private readonly ISettingsService _settingsService;
    
    public SettingsWindowViewModel(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }
}