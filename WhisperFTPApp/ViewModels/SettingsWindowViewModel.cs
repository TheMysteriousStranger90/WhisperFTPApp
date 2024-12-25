using System.Reactive;
using System.Threading.Tasks;
using ReactiveUI;
using WhisperFTPApp.Services.Interfaces;
using WhisperFTPApp.Settings;

namespace WhisperFTPApp.ViewModels;

public class SettingsWindowViewModel : ViewModelBase
{
    private readonly ISettingsService _settingsService;
    private readonly IBackgroundService _backgroundService;
    private string _selectedBackground;
    
    public BackgroundSettings BackgroundSettings { get; } = new();
    
    public string SelectedBackground
    {
        get => _selectedBackground;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedBackground, value);
            _ = _backgroundService.ChangeBackgroundAsync(value);
        }
    }

    public SettingsWindowViewModel(ISettingsService settingsService, IBackgroundService backgroundService)
    {
        _settingsService = settingsService;
        _backgroundService = backgroundService;
        _selectedBackground = _backgroundService.CurrentBackground;
    }

    
    /*
    private readonly ISettingsService _settingsService;
    private string _selectedBackground;
    public BackgroundSettings BackgroundSettings { get; } = new();
    
    public string SelectedBackground
    {
        get => _selectedBackground;
        set => this.RaiseAndSetIfChanged(ref _selectedBackground, value);
    }

    public ReactiveCommand<string, Unit> SetBackgroundCommand { get; }

    public SettingsWindowViewModel(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        _selectedBackground = BackgroundSettings.SelectedBackground;
        
        SetBackgroundCommand = ReactiveCommand.Create<string>(async path =>
        {
            SelectedBackground = path;
            await SaveBackgroundSettingAsync(path);
        });
        
        _ = LoadBackgroundSettingAsync();
    }

    private async Task LoadBackgroundSettingAsync()
    {
        SelectedBackground = await _settingsService.LoadBackgroundSettingAsync();
    }

    private async Task SaveBackgroundSettingAsync(string background)
    {
        await _settingsService.SaveBackgroundSettingAsync(background);
    }
    */
}