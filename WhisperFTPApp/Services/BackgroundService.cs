using System.Reactive.Subjects;
using WhisperFTPApp.Logger;
using WhisperFTPApp.Services.Interfaces;

namespace WhisperFTPApp.Services;

public class BackgroundService : IBackgroundService, IDisposable
{
    private readonly ISettingsService _settingsService;
    private readonly BehaviorSubject<string> _backgroundChanged;
    private bool _disposed;

    public string CurrentBackground => _backgroundChanged.Value;
    public IObservable<string> BackgroundChanged => _backgroundChanged;

    public BackgroundService(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        _backgroundChanged =
            new BehaviorSubject<string>("avares://WhisperFTPApp/Assets/Image (3).jpg"); // Use full Avalonia URI
        _ = LoadInitialBackground();
    }

    private async Task LoadInitialBackground()
    {
        try
        {
            var background = await _settingsService.LoadBackgroundSettingAsync().ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(background) && !background.StartsWith("avares://", StringComparison.Ordinal))
            {
                background = $"avares://WhisperFTPApp{background}";
            }

            _backgroundChanged.OnNext(background);
        }
        catch (Exception ex)
        {
            StaticFileLogger.LogError($"[BackgroundService] Error loading background: {ex.Message}");
        }
    }

    public async Task ChangeBackgroundAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path cannot be null or empty.", nameof(path));
        }

        try
        {
            var dbPath = path.StartsWith("avares://", StringComparison.Ordinal)
                ? path
                : $"avares://WhisperFTPApp{path}";
            StaticFileLogger.LogInformation($"[BackgroundService] Changing background to: {dbPath}");
            await _settingsService.SaveBackgroundSettingAsync(dbPath).ConfigureAwait(false);
            _backgroundChanged.OnNext(dbPath);
        }
        catch (Exception ex)
        {
            StaticFileLogger.LogError($"[BackgroundService] Error changing background: {ex.Message}");
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _backgroundChanged.Dispose();
            }

            _disposed = true;
        }
    }
}
