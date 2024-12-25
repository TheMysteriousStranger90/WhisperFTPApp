using System;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using WhisperFTPApp.Services.Interfaces;

namespace WhisperFTPApp.Services;

public class BackgroundService : IBackgroundService
{
    private readonly ISettingsService _settingsService;
    private readonly BehaviorSubject<string> _backgroundChanged;

    public string CurrentBackground => _backgroundChanged.Value;
    public IObservable<string> BackgroundChanged => _backgroundChanged;

    public BackgroundService(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        _backgroundChanged = new BehaviorSubject<string>("/Assets/Image (3).jpg");
        _ = LoadInitialBackground();
    }

    private async Task LoadInitialBackground()
    {
        try
        {
            var background = await _settingsService.LoadBackgroundSettingAsync();
            Console.WriteLine($"[BackgroundService] Loaded initial background: {background}");
            _backgroundChanged.OnNext(background);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[BackgroundService] Error loading background: {ex.Message}");
        }
    }

    public async Task ChangeBackgroundAsync(string path)
    {
        try
        {
            var dbPath = path.StartsWith("avares://") ? path : $"avares://WhisperFTPApp{path}";
            Console.WriteLine($"[BackgroundService] Changing background to: {dbPath}");
            await _settingsService.SaveBackgroundSettingAsync(dbPath);
            _backgroundChanged.OnNext(dbPath);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[BackgroundService] Error changing background: {ex.Message}");
        }
    }
}