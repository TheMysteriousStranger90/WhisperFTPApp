namespace WhisperFTPApp.Services.Interfaces;

public interface IBackgroundService
{
    string CurrentBackground { get; }
    IObservable<string> BackgroundChanged { get; }
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task ChangeBackgroundAsync(string path, CancellationToken cancellationToken = default);
}
