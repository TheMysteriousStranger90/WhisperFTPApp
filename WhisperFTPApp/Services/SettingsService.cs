using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WhisperFTPApp.Constants;
using WhisperFTPApp.Data;
using WhisperFTPApp.Logger;
using WhisperFTPApp.Models;
using WhisperFTPApp.Services.Interfaces;

namespace WhisperFTPApp.Services;

internal sealed class SettingsService : ISettingsService, IDisposable
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly ICredentialEncryption _encryption;
    private readonly string _settingsFilePath;
    private readonly SemaphoreSlim _fileLock = new(1, 1);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public SettingsService(
        IDbContextFactory<AppDbContext> contextFactory,
        ICredentialEncryption encryption,
        IPathManager pathManager)
    {
        _contextFactory = contextFactory;
        _encryption = encryption;
        _settingsFilePath = pathManager.GetSettingsFilePath();
    }

    // ── FTP Connections (Database) ──────────────────────────────────

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

    // ── Background (settings.json) ─────────────────────────────────

    public async Task SaveBackgroundSettingAsync(string backgroundPath, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(backgroundPath);

        var dbPath = ConvertToDbPath(backgroundPath);

        var settings = await LoadAppSettingsAsync(cancellationToken).ConfigureAwait(false);
        settings.BackgroundPathImage = dbPath;
        await SaveAppSettingsAsync(settings, cancellationToken).ConfigureAwait(false);

        StaticFileLogger.LogInformation($"Background path saved: {dbPath}");
    }

    public async Task<string> LoadBackgroundSettingAsync(CancellationToken cancellationToken = default)
    {
        var settings = await LoadAppSettingsAsync(cancellationToken).ConfigureAwait(false);
        var dbPath = settings.BackgroundPathImage;

        var avaresPath = ConvertToAvaresPath(dbPath);
        StaticFileLogger.LogInformation($"Background path loaded: {avaresPath}");

        return avaresPath;
    }

    // ── Language (settings.json) ───────────────────────────────────

    public async Task SaveLanguageSettingAsync(string language, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(language);

        var settings = await LoadAppSettingsAsync(cancellationToken).ConfigureAwait(false);
        settings.Language = language;
        await SaveAppSettingsAsync(settings, cancellationToken).ConfigureAwait(false);

        StaticFileLogger.LogInformation($"Language saved: {language}");
    }

    public async Task<string> LoadLanguageSettingAsync(CancellationToken cancellationToken = default)
    {
        var settings = await LoadAppSettingsAsync(cancellationToken).ConfigureAwait(false);
        StaticFileLogger.LogInformation($"Language loaded: {settings.Language}");
        return settings.Language;
    }

    // ── JSON file helpers ──────────────────────────────────────────

    private async Task<AppSettings> LoadAppSettingsAsync(CancellationToken cancellationToken)
    {
        await _fileLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(_settingsFilePath))
                return new AppSettings();

            var json = await File.ReadAllTextAsync(_settingsFilePath, cancellationToken).ConfigureAwait(false);
            return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
        }
        catch (Exception ex)
        {
            StaticFileLogger.LogError($"Error loading settings.json: {ex.Message}");
            return new AppSettings();
        }
        finally
        {
            _fileLock.Release();
        }
    }

    private async Task SaveAppSettingsAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        await _fileLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            await File.WriteAllTextAsync(_settingsFilePath, json, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            StaticFileLogger.LogError($"Error saving settings.json: {ex.Message}");
            throw;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public void Dispose()
    {
        _fileLock.Dispose();
    }

    // ── Path converters ────────────────────────────────────────────

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
