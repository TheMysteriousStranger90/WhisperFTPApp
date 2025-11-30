using Microsoft.EntityFrameworkCore;
using WhisperFTPApp.Data;
using WhisperFTPApp.Logger;
using WhisperFTPApp.Models;
using WhisperFTPApp.Services.Interfaces;

namespace WhisperFTPApp.Services;

internal sealed class SettingsService : ISettingsService
{
    private readonly AppDbContext _context;

    public SettingsService(AppDbContext context)
    {
        _context = context;
    }

    public async Task SaveConnectionsAsync(IEnumerable<FtpConnectionEntity> connections,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connections);

        var connectionsList = connections.ToList();

        var transaction = await _context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using (transaction)
        try
        {
            StaticFileLogger.LogInformation($"Saving {connectionsList.Count} connections to database");

            var existing = await _context.FtpConnections.ToListAsync(cancellationToken).ConfigureAwait(false);
            if (existing.Count != 0)
            {
                _context.FtpConnections.RemoveRange(existing);
                await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }

            foreach (var connection in connectionsList)
            {
                var newConnection = new FtpConnectionEntity
                {
                    Name = connection.Name,
                    Address = connection.Address,
                    Username = connection.Username,
                    Password = connection.Password,
                    LastUsed = connection.LastUsed
                };

                _context.FtpConnections.Add(newConnection);
            }

            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

            StaticFileLogger.LogInformation("Connections saved successfully");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            StaticFileLogger.LogError($"Database error: {ex.Message}");
            throw;
        }
    }

    public async Task<List<FtpConnectionEntity>> LoadConnectionsAsync(CancellationToken cancellationToken = default)
    {
        return await _context.FtpConnections
            .Select(e => new FtpConnectionEntity
            {
                Name = e.Name,
                Address = e.Address,
                Username = e.Username,
                Password = e.Password,
                LastUsed = e.LastUsed
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task DeleteConnectionAsync(FtpConnectionEntity connection,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);

        var entity = await _context.FtpConnections
            .FirstOrDefaultAsync(x => x.Address == connection.Address, cancellationToken)
            .ConfigureAwait(false);

        if (entity != null)
        {
            _context.FtpConnections.Remove(entity);
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task SaveBackgroundSettingAsync(string backgroundPath, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(backgroundPath);

        var dbPath = ConvertToDbPath(backgroundPath);

        var settings = await _context.Settings.FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (settings == null)
        {
            settings = new SettingsEntity { BackgroundPathImage = dbPath };
            _context.Settings.Add(settings);
        }
        else
        {
            settings.BackgroundPathImage = dbPath;
        }

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        StaticFileLogger.LogInformation($"Background path saved: {dbPath}");
    }

    public async Task<string> LoadBackgroundSettingAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _context.Settings.FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        var dbPath = settings?.BackgroundPathImage ?? "/Assets/Image (3).jpg";

        var avaresPath = ConvertToAvaresPath(dbPath);
        StaticFileLogger.LogInformation($"Background path loaded: {avaresPath}");

        return avaresPath;
    }

    private static string ConvertToDbPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return "/Assets/Image (3).jpg";

        if (path.StartsWith("avares://AzioWhisperFTP", StringComparison.OrdinalIgnoreCase))
        {
            return path.Replace("avares:/AzioWhisperFTP", "", StringComparison.OrdinalIgnoreCase);
        }

        return path.StartsWith('/') ? path : $"/{path}";
    }

    private static string ConvertToAvaresPath(string dbPath)
    {
        if (string.IsNullOrWhiteSpace(dbPath))
            return "avares://AzioWhisperFTP/Assets/Image (3).jpg";

        if (dbPath.StartsWith("avares://", StringComparison.OrdinalIgnoreCase))
        {
            return dbPath;
        }

        var relativePath = dbPath.TrimStart('/');

        return $"avares://AzioWhisperFTP/{relativePath}";
    }
}
