using System.Reactive.Subjects;
using WhisperFTPApp.Constants;
using WhisperFTPApp.Logger;
using WhisperFTPApp.Services.Interfaces;

namespace WhisperFTPApp.Services;

public class BackgroundService : IBackgroundService, IDisposable
{
    private readonly ISettingsService _settingsService;
    private readonly BehaviorSubject<string> _backgroundChanged;
    private bool _disposed;
    private bool _initialized;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public string CurrentBackground => _backgroundChanged.Value;
    public IObservable<string> BackgroundChanged => _backgroundChanged;

    public BackgroundService(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        _backgroundChanged = new BehaviorSubject<string>(AppConstants.DefaultBackground);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized) return;

        await _initLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_initialized) return;

            var background = await _settingsService.LoadBackgroundSettingAsync(cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(background))
            {
                if (!background.StartsWith("avares://", StringComparison.Ordinal))
                {
                    background = $"{AppConstants.AvaresPrefix}{background}";
                }
                _backgroundChanged.OnNext(background);
            }

            _initialized = true;
        }
        catch (Exception ex)
        {
            StaticFileLogger.LogError($"[BackgroundService] Error loading background: {ex.Message}");
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async Task ChangeBackgroundAsync(string path, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path cannot be null or empty.", nameof(path));
        }

        try
        {
            var dbPath = path.StartsWith("avares://", StringComparison.Ordinal)
                ? path
                : $"{AppConstants.AvaresPrefix}{path}";
            StaticFileLogger.LogInformation($"[BackgroundService] Changing background to: {dbPath}");
            await _settingsService.SaveBackgroundSettingAsync(dbPath, cancellationToken).ConfigureAwait(false);
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
                _initLock.Dispose();
            }

            _disposed = true;
        }
    }
}
