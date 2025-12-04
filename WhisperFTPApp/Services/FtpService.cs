using FluentFTP;
using WhisperFTPApp.Configurations;
using WhisperFTPApp.Logger;
using WhisperFTPApp.Models;
using WhisperFTPApp.Services.Interfaces;

namespace WhisperFTPApp.Services;

internal sealed class FtpService : IFtpService, IDisposable
{
    private AsyncFtpClient? _client;
    private const int MaxRetries = 3;
    private const int BaseDelay = 2000;

    #region Connection Management

    public async Task<bool> ConnectAsync(FtpConfiguration configuration, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        StaticFileLogger.LogInformation($"Attempting to connect to {configuration.FtpAddress}");

        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                StaticFileLogger.LogInformation($"Connection attempt {attempt} of {MaxRetries}");

                _client = CreateClient(configuration);

                await _client.Connect(cancellationToken).ConfigureAwait(false);

                if (_client.IsConnected)
                {
                    StaticFileLogger.LogInformation("Connection successful");
                    return true;
                }

                StaticFileLogger.LogError($"Connection attempt {attempt} failed");

                if (attempt < MaxRetries)
                {
                    int delay = BaseDelay * attempt;
                    StaticFileLogger.LogInformation($"Waiting {delay}ms before retry");
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                StaticFileLogger.LogInformation("Connection cancelled by user");
                throw;
            }
            catch (Exception ex)
            {
                StaticFileLogger.LogError($"Connection attempt {attempt} failed: {ex.Message}");

                if (attempt >= MaxRetries)
                {
                    StaticFileLogger.LogError("Max retries reached");
                    return false;
                }

                int delay = BaseDelay * attempt;
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
        }

        StaticFileLogger.LogError("All connection attempts failed");
        return false;
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        if (_client != null)
        {
            await _client.Disconnect(cancellationToken).ConfigureAwait(false);
            await _client.DisposeAsync().ConfigureAwait(false);
            _client = null;
        }
        StaticFileLogger.LogInformation("Disconnected from FTP server");
    }

    public async Task<bool> TestSecureConnectionAsync(FtpConfiguration configuration, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        StaticFileLogger.LogInformation("Testing SSL/TLS connection");

        try
        {
            using var testClient = CreateClient(configuration);
            testClient.Config.EncryptionMode = FtpEncryptionMode.Explicit;

            await testClient.Connect(cancellationToken).ConfigureAwait(false);

            if (testClient.IsConnected)
            {
                StaticFileLogger.LogInformation("SSL connection successful");
                await testClient.Disconnect(cancellationToken).ConfigureAwait(false);
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            StaticFileLogger.LogError($"SSL connection failed: {ex.Message}");
            return false;
        }
    }

    #endregion

    #region Directory Operations

    public async Task<IEnumerable<FileSystemItem>> ListDirectoryAsync(
        FtpConfiguration configuration,
        string path = "/",
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(path);

        StaticFileLogger.LogInformation($"Listing directory: {path}");

        try
        {
            var client = await GetOrCreateClient(configuration, cancellationToken).ConfigureAwait(false);
            var items = await client.GetListing(path, cancellationToken).ConfigureAwait(false);

            var result = items.Select(item => new FileSystemItem
            {
                Name = item.Name,
                FullPath = item.FullName,
                IsDirectory = item.Type == FtpObjectType.Directory,
                Size = item.Size,
                Modified = item.Modified,
                Type = item.Type == FtpObjectType.Directory ? "Directory" : Path.GetExtension(item.Name)
            }).ToList();

            StaticFileLogger.LogInformation($"Listed {result.Count} items in directory {path}");
            return result;
        }
        catch (Exception ex)
        {
            StaticFileLogger.LogError($"Failed to list directory {path}: {ex.Message}");
            throw;
        }
    }

    public async Task CreateDirectoryAsync(
        FtpConfiguration configuration,
        string remotePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(remotePath);

        StaticFileLogger.LogInformation($"Creating directory: {remotePath}");

        try
        {
            var client = await GetOrCreateClient(configuration, cancellationToken).ConfigureAwait(false);
            await client.CreateDirectory(remotePath, cancellationToken).ConfigureAwait(false);

            StaticFileLogger.LogInformation($"Directory created: {remotePath}");
        }
        catch (Exception ex)
        {
            StaticFileLogger.LogError($"Failed to create directory {remotePath}: {ex.Message}");
            throw;
        }
    }

    public async Task DeleteDirectoryAsync(
        FtpConfiguration configuration,
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(path);

        StaticFileLogger.LogInformation($"Starting recursive delete of directory: {path}");

        try
        {
            var client = await GetOrCreateClient(configuration, cancellationToken).ConfigureAwait(false);
            await client.DeleteDirectory(path, cancellationToken).ConfigureAwait(false);

            StaticFileLogger.LogInformation($"Directory deleted successfully: {path}");
        }
        catch (Exception ex)
        {
            StaticFileLogger.LogError($"Delete directory failed {path}: {ex.Message}");
            throw;
        }
    }

    public async Task<bool> DirectoryExistsAsync(
        FtpConfiguration configuration,
        string remotePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(remotePath);

        try
        {
            var client = await GetOrCreateClient(configuration, cancellationToken).ConfigureAwait(false);
            return await client.DirectoryExists(remotePath, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            return false;
        }
    }

    public async Task<string> GetWorkingDirectoryAsync(
        FtpConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        try
        {
            var client = await GetOrCreateClient(configuration, cancellationToken).ConfigureAwait(false);
            var directory = await client.GetWorkingDirectory(cancellationToken).ConfigureAwait(false);

            StaticFileLogger.LogInformation($"Current working directory: {directory}");
            return directory;
        }
        catch (Exception ex)
        {
            StaticFileLogger.LogError($"Failed to get working directory: {ex.Message}");
            return "/";
        }
    }

    #endregion

    #region File Operations

    public async Task UploadFileAsync(
        FtpConfiguration configuration,
        string localPath,
        string remotePath,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(localPath);
        ArgumentNullException.ThrowIfNull(remotePath);

        StaticFileLogger.LogInformation($"Starting upload: {localPath} -> {remotePath}");

        try
        {
            var client = await GetOrCreateClient(configuration, cancellationToken).ConfigureAwait(false);

            Progress<FtpProgress>? ftpProgress = null;
            if (progress != null)
            {
                ftpProgress = new Progress<FtpProgress>(p => progress.Report(p.Progress));
            }

            var result = await client.UploadFile(
                localPath,
                remotePath,
                FtpRemoteExists.Overwrite,
                true,
                FtpVerify.None,
                ftpProgress,
                cancellationToken).ConfigureAwait(false);

            if (result == FtpStatus.Success)
            {
                StaticFileLogger.LogInformation($"Upload completed: {remotePath}");
            }
            else
            {
                StaticFileLogger.LogWarning($"Upload finished with status: {result}");
            }
        }
        catch (Exception ex)
        {
            StaticFileLogger.LogError($"Upload failed {remotePath}: {ex.Message}");
            throw;
        }
    }

    public async Task UploadFileWithResumeAsync(
        FtpConfiguration configuration,
        string localPath,
        string remotePath,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(localPath);
        ArgumentNullException.ThrowIfNull(remotePath);

        StaticFileLogger.LogInformation($"Starting upload with resume support: {localPath} -> {remotePath}");

        try
        {
            var client = await GetOrCreateClient(configuration, cancellationToken).ConfigureAwait(false);

            Progress<FtpProgress>? ftpProgress = null;
            if (progress != null)
            {
                ftpProgress = new Progress<FtpProgress>(p => progress.Report(p.Progress));
            }

            var result = await client.UploadFile(
                localPath,
                remotePath,
                FtpRemoteExists.Resume,
                true,
                FtpVerify.None,
                ftpProgress,
                cancellationToken).ConfigureAwait(false);

            if (result == FtpStatus.Success || result == FtpStatus.Skipped)
            {
                StaticFileLogger.LogInformation($"Upload with resume completed: {remotePath}");
            }
            else
            {
                StaticFileLogger.LogWarning($"Upload finished with status: {result}");
            }
        }
        catch (Exception ex)
        {
            StaticFileLogger.LogError($"Upload with resume failed: {ex.Message}");
            throw;
        }
    }

    public async Task DownloadFileAsync(
        FtpConfiguration configuration,
        string remotePath,
        string localPath,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(remotePath);
        ArgumentNullException.ThrowIfNull(localPath);

        StaticFileLogger.LogInformation($"Starting download: {remotePath} -> {localPath}");

        try
        {
            var client = await GetOrCreateClient(configuration, cancellationToken).ConfigureAwait(false);

            Progress<FtpProgress>? ftpProgress = null;
            if (progress != null)
            {
                ftpProgress = new Progress<FtpProgress>(p => progress.Report(p.Progress));
            }

            var result = await client.DownloadFile(
                localPath,
                remotePath,
                FtpLocalExists.Overwrite,
                FtpVerify.None,
                ftpProgress,
                cancellationToken).ConfigureAwait(false);

            if (result == FtpStatus.Success)
            {
                StaticFileLogger.LogInformation($"Download completed: {localPath}");
            }
            else
            {
                StaticFileLogger.LogWarning($"Download finished with status: {result}");
            }
        }
        catch (Exception ex)
        {
            StaticFileLogger.LogError($"Download failed {remotePath}: {ex.Message}");

            if (File.Exists(localPath))
            {
                try
                {
                    File.Delete(localPath);
                    StaticFileLogger.LogInformation($"Deleted incomplete file: {localPath}");
                }
                catch (Exception deleteEx)
                {
                    StaticFileLogger.LogError($"Failed to delete incomplete file: {deleteEx.Message}");
                }
            }

            throw;
        }
    }

    public async Task DeleteFileAsync(
        FtpConfiguration configuration,
        string remotePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(remotePath);

        StaticFileLogger.LogInformation($"Deleting file: {remotePath}");

        try
        {
            var client = await GetOrCreateClient(configuration, cancellationToken).ConfigureAwait(false);
            await client.DeleteFile(remotePath, cancellationToken).ConfigureAwait(false);

            StaticFileLogger.LogInformation($"File deleted successfully: {remotePath}");
        }
        catch (Exception ex)
        {
            StaticFileLogger.LogError($"Delete file failed {remotePath}: {ex.Message}");
            throw;
        }
    }

    public async Task RenameAsync(
        FtpConfiguration configuration,
        string currentName,
        string newName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(currentName);
        ArgumentNullException.ThrowIfNull(newName);

        StaticFileLogger.LogInformation($"Renaming {currentName} to {newName}");

        try
        {
            var client = await GetOrCreateClient(configuration, cancellationToken).ConfigureAwait(false);
            await client.Rename(currentName, newName, cancellationToken).ConfigureAwait(false);

            StaticFileLogger.LogInformation("Renamed successfully");
        }
        catch (Exception ex)
        {
            StaticFileLogger.LogError($"Rename failed: {ex.Message}");
            throw;
        }
    }

    #endregion

    #region File Information

    public async Task<bool> FileExistsAsync(
        FtpConfiguration configuration,
        string remotePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(remotePath);

        try
        {
            var client = await GetOrCreateClient(configuration, cancellationToken).ConfigureAwait(false);
            return await client.FileExists(remotePath, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            return false;
        }
    }

    public async Task<long> GetFileSizeAsync(
        FtpConfiguration configuration,
        string remotePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(remotePath);

        try
        {
            var client = await GetOrCreateClient(configuration, cancellationToken).ConfigureAwait(false);
            return await client.GetFileSize(remotePath, -1, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            StaticFileLogger.LogError($"Failed to get file size for {remotePath}: {ex.Message}");
            return 0;
        }
    }

    public async Task<DateTime> GetFileModifiedTimeAsync(
        FtpConfiguration configuration,
        string remotePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(remotePath);

        try
        {
            var client = await GetOrCreateClient(configuration, cancellationToken).ConfigureAwait(false);
            return await client.GetModifiedTime(remotePath, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            StaticFileLogger.LogError($"Failed to get modified time for {remotePath}: {ex.Message}");
            return DateTime.MinValue;
        }
    }

    public async Task<FileSystemItem> GetFileDetailsAsync(
        FtpConfiguration configuration,
        string remotePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(remotePath);

        try
        {
            var client = await GetOrCreateClient(configuration, cancellationToken).ConfigureAwait(false);
            var size = await client.GetFileSize(remotePath, -1, cancellationToken).ConfigureAwait(false);
            var modified = await client.GetModifiedTime(remotePath, cancellationToken).ConfigureAwait(false);

            var fileItem = new FileSystemItem
            {
                Name = Path.GetFileName(remotePath),
                FullPath = remotePath,
                IsDirectory = false,
                Size = size,
                Modified = modified,
                Type = Path.GetExtension(remotePath)
            };

            StaticFileLogger.LogInformation(
                $"File details retrieved - Name: {fileItem.Name}, Size: {fileItem.Size}, Modified: {fileItem.Modified}");

            return fileItem;
        }
        catch (Exception ex)
        {
            StaticFileLogger.LogError($"Failed to get file details: {ex.Message}");
            throw;
        }
    }

    #endregion

    #region Helper Methods

    private static AsyncFtpClient CreateClient(FtpConfiguration configuration)
    {
        var uri = new Uri(configuration.FtpAddress.StartsWith("ftp://", StringComparison.OrdinalIgnoreCase)
            ? configuration.FtpAddress
            : $"ftp://{configuration.FtpAddress}");

        var client = new AsyncFtpClient(uri.Host, configuration.Username, configuration.Password, configuration.Port);

        // Configure client
        client.Config.ConnectTimeout = configuration.Timeout > 0 ? configuration.Timeout : 15000;
        client.Config.DataConnectionReadTimeout = configuration.ReadWriteTimeout > 0
            ? configuration.ReadWriteTimeout
            : 30000;
        client.Config.DataConnectionConnectTimeout = configuration.ReadWriteTimeout > 0
            ? configuration.ReadWriteTimeout
            : 30000;

        client.Config.EncryptionMode = configuration.EnableSsl
            ? FtpEncryptionMode.Explicit
            : FtpEncryptionMode.None;

        if (configuration.EnableSsl)
        {
            client.Config.ValidateAnyCertificate = true;
        }

        client.Config.DataConnectionType = configuration.UsePassive
            ? FtpDataConnectionType.AutoPassive
            : FtpDataConnectionType.AutoActive;

        client.Config.RetryAttempts = configuration.MaxRetries;
        client.Config.SocketKeepAlive = true;

        StaticFileLogger.LogInformation(
            $"Client created - Host: {uri.Host}, Port: {configuration.Port}, SSL: {configuration.EnableSsl}, Passive: {configuration.UsePassive}");

        return client;
    }

    private async Task<AsyncFtpClient> GetOrCreateClient(FtpConfiguration configuration, CancellationToken cancellationToken)
    {
        if (_client == null || !_client.IsConnected)
        {
            await ConnectAsync(configuration, cancellationToken).ConfigureAwait(false);
        }

        return _client ?? throw new InvalidOperationException("Failed to create FTP client");
    }

    #endregion

    public void Dispose()
    {
        _client?.Dispose();
        _client = null;
    }
}
