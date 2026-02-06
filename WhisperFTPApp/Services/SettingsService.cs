using Microsoft.EntityFrameworkCore;
using WhisperFTPApp.Constants;
using WhisperFTPApp.Data;
using WhisperFTPApp.Logger;
using WhisperFTPApp.Models;
using WhisperFTPApp.Services.Interfaces;

namespace WhisperFTPApp.Services;

internal sealed class SettingsService : ISettingsService
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly ICredentialEncryption _encryption;

    public SettingsService(IDbContextFactory<AppDbContext> contextFactory, ICredentialEncryption encryption)
    {
        _contextFactory = contextFactory;
        _encryption = encryption;
    }

    public async Task SaveConnectionsAsync(IEnumerable<FtpConnectionEntity> connections,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connections);

        var connectionsList = connections.ToList();

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var transaction = await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        await using (transaction)
        {
            try
            {
                StaticFileLogger.LogInformation($"Saving {connectionsList.Count} connections to database");

                var existing = await context.FtpConnections.ToListAsync(cancellationToken).ConfigureAwait(false);
                if (existing.Count != 0)
                {
                    context.FtpConnections.RemoveRange(existing);
                    await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                }

                foreach (var connection in connectionsList)
                {
                    var newConnection = new FtpConnectionEntity
                    {
                        Name = connection.Name,
                        Address = connection.Address,
                        Username = connection.Username,
                        Password = _encryption.Encrypt(connection.Password),
                        LastUsed = connection.LastUsed
                    };

                    context.FtpConnections.Add(newConnection);
                }

                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
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
    }

    public async Task<List<FtpConnectionEntity>> LoadConnectionsAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var entities = await context.FtpConnections.ToListAsync(cancellationToken).ConfigureAwait(false);

        return entities.Select(e => new FtpConnectionEntity
        {
            Name = e.Name,
            Address = e.Address,
            Username = e.Username,
            Password = _encryption.Decrypt(e.Password),
            LastUsed = e.LastUsed
        }).ToList();
    }

    public async Task DeleteConnectionAsync(FtpConnectionEntity connection,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var entity = await context.FtpConnections
            .FirstOrDefaultAsync(x => x.Address == connection.Address, cancellationToken)
            .ConfigureAwait(false);

        if (entity != null)
        {
            context.FtpConnections.Remove(entity);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task SaveBackgroundSettingAsync(string backgroundPath, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(backgroundPath);

        var dbPath = ConvertToDbPath(backgroundPath);

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var settings = await context.Settings.FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (settings == null)
        {
            settings = new SettingsEntity { BackgroundPathImage = dbPath };
            context.Settings.Add(settings);
        }
        else
        {
            settings.BackgroundPathImage = dbPath;
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        StaticFileLogger.LogInformation($"Background path saved: {dbPath}");
    }

    public async Task<string> LoadBackgroundSettingAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var settings = await context.Settings.FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        var dbPath = settings?.BackgroundPathImage ?? "/Assets/Image (3).jpg";

        var avaresPath = ConvertToAvaresPath(dbPath);
        StaticFileLogger.LogInformation($"Background path loaded: {avaresPath}");

        return avaresPath;
    }

    private static string ConvertToDbPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return "/Assets/Image (3).jpg";

        if (path.StartsWith(AppConstants.AvaresPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return path.Replace(AppConstants.AvaresPrefix, "", StringComparison.OrdinalIgnoreCase);
        }

        return path.StartsWith('/') ? path : $"/{path}";
    }

    private static string ConvertToAvaresPath(string dbPath)
    {
        if (string.IsNullOrWhiteSpace(dbPath))
            return AppConstants.DefaultBackground;

        if (dbPath.StartsWith("avares://", StringComparison.OrdinalIgnoreCase))
        {
            return dbPath;
        }

        var relativePath = dbPath.TrimStart('/');

        return $"{AppConstants.AvaresPrefix}/{relativePath}";
    }
}
