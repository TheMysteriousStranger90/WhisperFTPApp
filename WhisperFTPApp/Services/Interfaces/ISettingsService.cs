using WhisperFTPApp.Models;

namespace WhisperFTPApp.Services.Interfaces;

public interface ISettingsService
{
    Task SaveConnectionsAsync(IEnumerable<FtpConnectionEntity> connections, CancellationToken cancellationToken = default);
    Task<List<FtpConnectionEntity>> LoadConnectionsAsync(CancellationToken cancellationToken = default);
    Task DeleteConnectionAsync(FtpConnectionEntity connection, CancellationToken cancellationToken = default);
    Task SaveBackgroundSettingAsync(string backgroundPath, CancellationToken cancellationToken = default);
    Task<string> LoadBackgroundSettingAsync(CancellationToken cancellationToken = default);
}
