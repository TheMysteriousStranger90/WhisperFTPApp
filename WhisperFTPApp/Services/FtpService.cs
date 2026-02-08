using FluentFTP;
using FluentFTP.Exceptions;
using WhisperFTPApp.Configurations;
using WhisperFTPApp.Logger;
using WhisperFTPApp.Models;
using WhisperFTPApp.Services.Interfaces;

namespace WhisperFTPApp.Services;

internal sealed class FtpService : IFtpService, IDisposable
{
    private AsyncFtpClient? _client;
    private readonly SemaphoreSlim _clientLock = new(1, 1);
    private const int MaxRetries = 3;
    private const int BaseDelay = 2000;
    private bool _disposed;

    public async Task<bool> ConnectAsync(FtpConfiguration configuration, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        StaticFileLogger.LogInformation($"Attempting to connect to {configuration.FtpAddress}");

        await _clientLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            for (int attempt = 1; attempt <= MaxRetries; attempt++)
            {
                try
                {
                    StaticFileLogger.LogInformation($"Connection attempt {attempt} of {MaxRetries}");

                    if (_client != null)
                    {
                        await _client.DisposeAsync().ConfigureAwait(false);
                        _client = null;
                    }

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
                catch (FtpAuthenticationException ex)
                {
                    StaticFileLogger.LogError($"Authentication failed: {ex.Message}");
                    return false;
                }
                catch (FtpSecurityNotAvailableException ex)
                {
                    StaticFileLogger.LogError($"SSL/TLS not supported by server: {ex.Message}");
                    return false;
                }
                catch (FtpMissingSocketException ex)
                {
                    StaticFileLogger.LogError($"Connection lost (socket): {ex.Message}");

                    if (attempt >= MaxRetries)
                    {
                        StaticFileLogger.LogError("Max retries reached after socket failures");
                        return false;
                    }

                    int delay = BaseDelay * attempt;
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }
                catch (FtpCommandException ex)
                {
                    StaticFileLogger.LogError(
                        $"FTP command error on connect (code {ex.CompletionCode}): {ex.Message}");

                    if (attempt >= MaxRetries)
                    {
                        StaticFileLogger.LogError("Max retries reached");
                        return false;
                    }

                    int delay = BaseDelay * attempt;
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
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
        finally
        {
            _clientLock.Release();
        }
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        await _clientLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_client != null)
            {
                await _client.Disconnect(cancellationToken).ConfigureAwait(false);
                await _client.DisposeAsync().ConfigureAwait(false);
                _client = null;
            }

            StaticFileLogger.LogInformation("Disconnected from FTP server");
        }
        finally
        {
            _clientLock.Release();
        }
    }

    public async Task<bool> TestSecureConnectionAsync(FtpConfiguration configuration,
        CancellationToken cancellationToken = default)
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
        catch (FtpSecurityNotAvailableException ex)
        {
            StaticFileLogger.LogError($"Server does not support SSL/TLS: {ex.Message}");
            return false;
        }
        catch (FtpAuthenticationException ex)
        {
            StaticFileLogger.LogError($"SSL authentication failed: {ex.Message}");
            return false;
        }
        catch (Exception ex)
        {
            StaticFileLogger.LogError($"SSL connection failed: {ex.Message}");
            return false;
        }
    }

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
            var client = await GetOrCreateClientAsync(configuration, cancellationToken).ConfigureAwait(false);
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
        catch (FtpCommandException ex) when (ex.CompletionCode == "550")
        {
            StaticFileLogger.LogError($"Access denied or directory not found {path}: {ex.Message}");
            throw;
        }
        catch (FtpMissingSocketException ex)
        {
            StaticFileLogger.LogError($"Connection lost while listing {path}: {ex.Message}");
            ResetClient();
            throw;
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
            var client = await GetOrCreateClientAsync(configuration, cancellationToken).ConfigureAwait(false);
            await client.CreateDirectory(remotePath, cancellationToken).ConfigureAwait(false);

            StaticFileLogger.LogInformation($"Directory created: {remotePath}");
        }
        catch (FtpCommandException ex) when (ex.CompletionCode == "550")
        {
            StaticFileLogger.LogInformation(
                $"Directory already exists or access denied: {remotePath} ({ex.CompletionCode})");
            throw;
        }
        catch (FtpCommandException ex)
        {
            StaticFileLogger.LogError($"FTP error creating directory {remotePath}: {ex.CompletionCode} - {ex.Message}");
            throw;
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
            var client = await GetOrCreateClientAsync(configuration, cancellationToken).ConfigureAwait(false);
            await client.DeleteDirectory(path, cancellationToken).ConfigureAwait(false);

            StaticFileLogger.LogInformation($"Directory deleted successfully: {path}");
        }
        catch (FtpCommandException ex) when (ex.CompletionCode is "550" or "450")
        {
            StaticFileLogger.LogError(
                $"Cannot delete directory {path}: {(ex.CompletionCode == "550" ? "access denied or not empty" : "directory busy")}");
            throw;
        }
        catch (FtpCommandException ex)
        {
            StaticFileLogger.LogError($"FTP error deleting directory {path}: {ex.CompletionCode} - {ex.Message}");
            throw;
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
            var client = await GetOrCreateClientAsync(configuration, cancellationToken).ConfigureAwait(false);
            return await client.DirectoryExists(remotePath, cancellationToken).ConfigureAwait(false);
        }
        catch (FtpCommandException ex)
        {
            StaticFileLogger.LogWarning($"FTP error checking directory {remotePath}: {ex.CompletionCode}");
            return false;
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
            var client = await GetOrCreateClientAsync(configuration, cancellationToken).ConfigureAwait(false);
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

        var fileInfo = new FileInfo(localPath);
        StaticFileLogger.LogInformation(
            $"Starting upload: {localPath} -> {remotePath} (Size: {fileInfo.Length} bytes)");

        int maxRetries = configuration.MaxRetries > 0 ? configuration.MaxRetries : MaxRetries;
        Exception? lastException = null;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                var client = await GetOrCreateClientAsync(configuration, cancellationToken).ConfigureAwait(false);

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
                    return;
                }

                var errorMsg = $"Upload returned status {result} for {remotePath} (attempt {attempt}/{maxRetries})";
                StaticFileLogger.LogError(errorMsg);
                lastException = new IOException(errorMsg);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                StaticFileLogger.LogInformation("Upload cancelled by user");
                throw;
            }
            catch (FtpCommandException ex) when (ex.CompletionCode is "552" or "553")
            {
                StaticFileLogger.LogError(
                    $"Upload permanently failed for {remotePath}: {ex.CompletionCode} - {ex.Message}");
                throw;
            }
            catch (FtpCommandException ex) when (ex.CompletionCode == "550")
            {
                StaticFileLogger.LogError($"Upload access denied for {remotePath}: {ex.Message}");
                throw;
            }
            catch (FtpMissingSocketException ex)
            {
                lastException = ex;
                StaticFileLogger.LogError(
                    $"Upload connection lost (attempt {attempt}/{maxRetries}) for {remotePath}: {ex.Message}");
                ResetClient();
            }
            catch (FtpCommandException ex)
            {
                lastException = ex;
                StaticFileLogger.LogError(
                    $"Upload FTP error (attempt {attempt}/{maxRetries}) for {remotePath}: {ex.CompletionCode} - {ex.Message}");
            }
            catch (Exception ex)
            {
                lastException = ex;
                StaticFileLogger.LogError(
                    $"Upload attempt {attempt}/{maxRetries} failed for {remotePath}: {ex.Message}");
            }

            if (attempt < maxRetries)
            {
                int delay = configuration.RetryDelay > 0 ? configuration.RetryDelay * attempt : BaseDelay * attempt;
                StaticFileLogger.LogInformation($"Retrying upload in {delay}ms...");
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                ResetClient();
            }
        }

        throw new IOException(
            $"Upload failed after {maxRetries} attempts for {remotePath}: " +
            $"{lastException?.Message ?? "Unknown error"}", lastException);
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
            var client = await GetOrCreateClientAsync(configuration, cancellationToken).ConfigureAwait(false);

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
        catch (FtpCommandException ex) when (ex.CompletionCode is "550" or "552" or "553")
        {
            StaticFileLogger.LogError(
                $"Upload with resume permanently failed for {remotePath}: {ex.CompletionCode} - {ex.Message}");
            throw;
        }
        catch (FtpMissingSocketException ex)
        {
            StaticFileLogger.LogError($"Upload with resume lost connection for {remotePath}: {ex.Message}");
            ResetClient();
            throw;
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

        int maxRetries = configuration.MaxRetries > 0 ? configuration.MaxRetries : MaxRetries;
        Exception? lastException = null;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                var client = await GetOrCreateClientAsync(configuration, cancellationToken).ConfigureAwait(false);

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
                    return;
                }

                var errorMsg = $"Download returned status {result} for {remotePath} (attempt {attempt}/{maxRetries})";
                StaticFileLogger.LogError(errorMsg);
                lastException = new IOException(errorMsg);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                CleanupIncompleteFile(localPath);
                throw;
            }
            catch (FtpCommandException ex) when (ex.CompletionCode == "550")
            {
                CleanupIncompleteFile(localPath);
                StaticFileLogger.LogError($"Download access denied or file not found {remotePath}: {ex.Message}");
                throw;
            }
            catch (FtpCommandException ex) when (ex.CompletionCode == "450")
            {
                lastException = ex;
                StaticFileLogger.LogWarning(
                    $"Download file busy (attempt {attempt}/{maxRetries}) for {remotePath}: {ex.Message}");
                CleanupIncompleteFile(localPath);
            }
            catch (FtpMissingSocketException ex)
            {
                lastException = ex;
                StaticFileLogger.LogError(
                    $"Download connection lost (attempt {attempt}/{maxRetries}) for {remotePath}: {ex.Message}");
                CleanupIncompleteFile(localPath);
                ResetClient();
            }
            catch (FtpCommandException ex)
            {
                lastException = ex;
                StaticFileLogger.LogError(
                    $"Download FTP error (attempt {attempt}/{maxRetries}) for {remotePath}: {ex.CompletionCode} - {ex.Message}");
                CleanupIncompleteFile(localPath);
            }
            catch (Exception ex)
            {
                lastException = ex;
                StaticFileLogger.LogError(
                    $"Download attempt {attempt}/{maxRetries} failed for {remotePath}: {ex.Message}");
                CleanupIncompleteFile(localPath);
            }

            if (attempt < maxRetries)
            {
                int delay = configuration.RetryDelay > 0 ? configuration.RetryDelay * attempt : BaseDelay * attempt;
                StaticFileLogger.LogInformation($"Retrying download in {delay}ms...");
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                ResetClient();
            }
        }

        CleanupIncompleteFile(localPath);
        throw new IOException(
            $"Download failed after {maxRetries} attempts for {remotePath}: " +
            $"{lastException?.Message ?? "Unknown error"}", lastException);
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
            var client = await GetOrCreateClientAsync(configuration, cancellationToken).ConfigureAwait(false);
            await client.DeleteFile(remotePath, cancellationToken).ConfigureAwait(false);

            StaticFileLogger.LogInformation($"File deleted successfully: {remotePath}");
        }
        catch (FtpCommandException ex) when (ex.CompletionCode is "550" or "450")
        {
            StaticFileLogger.LogError(
                $"Cannot delete file {remotePath}: {(ex.CompletionCode == "550" ? "access denied or not found" : "file busy")}");
            throw;
        }
        catch (FtpCommandException ex)
        {
            StaticFileLogger.LogError($"FTP error deleting file {remotePath}: {ex.CompletionCode} - {ex.Message}");
            throw;
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
            var client = await GetOrCreateClientAsync(configuration, cancellationToken).ConfigureAwait(false);
            await client.Rename(currentName, newName, cancellationToken).ConfigureAwait(false);

            StaticFileLogger.LogInformation("Renamed successfully");
        }
        catch (FtpCommandException ex)
        {
            var reason = ex.CompletionCode switch
            {
                "550" => "source not found or access denied",
                "553" => "target file name not allowed",
                _ => $"FTP error {ex.CompletionCode}"
            };
            StaticFileLogger.LogError($"Rename failed ({currentName} -> {newName}): {reason} - {ex.Message}");
            throw;
        }
        catch (Exception ex)
        {
            StaticFileLogger.LogError($"Rename failed: {ex.Message}");
            throw;
        }
    }

    public async Task<bool> FileExistsAsync(
        FtpConfiguration configuration,
        string remotePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(remotePath);

        try
        {
            var client = await GetOrCreateClientAsync(configuration, cancellationToken).ConfigureAwait(false);
            return await client.FileExists(remotePath, cancellationToken).ConfigureAwait(false);
        }
        catch (FtpCommandException ex)
        {
            StaticFileLogger.LogWarning($"FTP error checking file existence {remotePath}: {ex.CompletionCode}");
            return false;
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
            var client = await GetOrCreateClientAsync(configuration, cancellationToken).ConfigureAwait(false);
            return await client.GetFileSize(remotePath, -1, cancellationToken).ConfigureAwait(false);
        }
        catch (FtpCommandException ex)
        {
            StaticFileLogger.LogError(
                $"FTP error getting file size for {remotePath}: {ex.CompletionCode} - {ex.Message}");
            return 0;
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
            var client = await GetOrCreateClientAsync(configuration, cancellationToken).ConfigureAwait(false);
            return await client.GetModifiedTime(remotePath, cancellationToken).ConfigureAwait(false);
        }
        catch (FtpCommandException ex)
        {
            StaticFileLogger.LogError(
                $"FTP error getting modified time for {remotePath}: {ex.CompletionCode} - {ex.Message}");
            return DateTime.MinValue;
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
            var client = await GetOrCreateClientAsync(configuration, cancellationToken).ConfigureAwait(false);
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
        catch (FtpCommandException ex) when (ex.CompletionCode == "550")
        {
            StaticFileLogger.LogError($"File not found or access denied: {remotePath}");
            throw;
        }
        catch (Exception ex)
        {
            StaticFileLogger.LogError($"Failed to get file details: {ex.Message}");
            throw;
        }
    }

    private static AsyncFtpClient CreateClient(FtpConfiguration configuration)
    {
        var uri = new Uri(configuration.FtpAddress.StartsWith("ftp://", StringComparison.OrdinalIgnoreCase)
            ? configuration.FtpAddress
            : $"ftp://{configuration.FtpAddress}");

        var client = new AsyncFtpClient(uri.Host, configuration.Username, configuration.Password, configuration.Port);

        client.Config.ConnectTimeout = configuration.Timeout > 0 ? configuration.Timeout : 30000;
        client.Config.DataConnectionReadTimeout = configuration.ReadWriteTimeout > 0
            ? configuration.ReadWriteTimeout
            : 60000;
        client.Config.DataConnectionConnectTimeout = configuration.ReadWriteTimeout > 0
            ? configuration.ReadWriteTimeout
            : 60000;

        client.Config.TransferChunkSize = configuration.BufferSize;
        client.Config.LocalFileBufferSize = configuration.BufferSize;

        if (configuration.EnableSsl)
        {
            client.Config.EncryptionMode = FtpEncryptionMode.Explicit;

            client.Config.ValidateAnyCertificate = configuration.AllowInvalidCertificates;

            if (!configuration.AllowInvalidCertificates)
            {
                client.ValidateCertificate += (control, e) =>
                {
                    if (e.PolicyErrors == System.Net.Security.SslPolicyErrors.None)
                    {
                        e.Accept = true;
                        return;
                    }

                    if (e.PolicyErrors.HasFlag(System.Net.Security.SslPolicyErrors.RemoteCertificateNameMismatch))
                    {
                        StaticFileLogger.LogError(
                            $"Certificate hostname mismatch! Expected: {uri.Host}, " +
                            $"Subject: {e.Certificate?.Subject}");
                        e.Accept = false;
                        return;
                    }

                    if (e.PolicyErrors.HasFlag(System.Net.Security.SslPolicyErrors.RemoteCertificateNotAvailable))
                    {
                        StaticFileLogger.LogError("Remote certificate not available");
                        e.Accept = false;
                        return;
                    }

                    if (e.PolicyErrors.HasFlag(System.Net.Security.SslPolicyErrors.RemoteCertificateChainErrors)
                        && e.Chain != null)
                    {
                        var toleratedFlags = new[]
                        {
                            System.Security.Cryptography.X509Certificates.X509ChainStatusFlags.NoError,
                            System.Security.Cryptography.X509Certificates.X509ChainStatusFlags.UntrustedRoot,
                            System.Security.Cryptography.X509Certificates.X509ChainStatusFlags.PartialChain,
                            System.Security.Cryptography.X509Certificates.X509ChainStatusFlags.RevocationStatusUnknown,
                            System.Security.Cryptography.X509Certificates.X509ChainStatusFlags.OfflineRevocation,
                            System.Security.Cryptography.X509Certificates.X509ChainStatusFlags.NotTimeValid,
                        };

                        var criticalErrors = e.Chain.ChainStatus
                            .Where(s => !toleratedFlags.Contains(s.Status))
                            .ToList();

                        if (criticalErrors.Count == 0)
                        {
                            if (e.Certificate != null)
                            {
                                var cert2 =
                                    new System.Security.Cryptography.X509Certificates.X509Certificate2(e.Certificate);

                                if (cert2.NotBefore <= DateTime.UtcNow && cert2.NotAfter >= DateTime.UtcNow)
                                {
                                    var chainWarnings = e.Chain.ChainStatus
                                        .Where(s => s.Status != System.Security.Cryptography.X509Certificates
                                            .X509ChainStatusFlags.NoError)
                                        .Select(s => s.Status.ToString());

                                    StaticFileLogger.LogInformation(
                                        $"Certificate accepted. Leaf cert valid: " +
                                        $"{cert2.NotBefore:yyyy-MM-dd} - {cert2.NotAfter:yyyy-MM-dd}. " +
                                        $"Tolerated chain warnings: [{string.Join(", ", chainWarnings)}]. " +
                                        $"Subject: {e.Certificate.Subject}");
                                    e.Accept = true;
                                    return;
                                }

                                StaticFileLogger.LogError(
                                    $"Server (leaf) certificate expired! " +
                                    $"Valid: {cert2.NotBefore:yyyy-MM-dd} - {cert2.NotAfter:yyyy-MM-dd}, " +
                                    $"Subject: {cert2.Subject}");
                                e.Accept = false;
                                return;
                            }

                            e.Accept = false;
                            return;
                        }

                        var errorDetails = string.Join(", ",
                            criticalErrors.Select(s => $"{s.Status}: {s.StatusInformation}"));
                        StaticFileLogger.LogError(
                            $"Certificate chain has critical errors: {errorDetails}. " +
                            $"Subject: {e.Certificate?.Subject}, Issuer: {e.Certificate?.Issuer}");
                        e.Accept = false;
                        return;
                    }

                    StaticFileLogger.LogError($"Certificate rejected: {e.PolicyErrors}");
                    e.Accept = false;
                };
            }
        }
        else
        {
            client.Config.EncryptionMode = FtpEncryptionMode.None;

            if (!configuration.Username.Equals("anonymous", StringComparison.OrdinalIgnoreCase))
            {
                StaticFileLogger.LogWarning(
                    "WARNING: Connecting without SSL/TLS. Credentials will be transmitted in plain text!");
            }
        }

        client.Config.DataConnectionType = configuration.UsePassive
            ? FtpDataConnectionType.AutoPassive
            : FtpDataConnectionType.AutoActive;

        client.Config.RetryAttempts = configuration.MaxRetries;
        client.Config.SocketKeepAlive = true;

        StaticFileLogger.LogInformation(
            $"Client created - Host: {uri.Host}, Port: {configuration.Port}, SSL: {configuration.EnableSsl}, " +
            $"Passive: {configuration.UsePassive}, BufferSize: {configuration.BufferSize}, " +
            $"CertValidation: {!configuration.AllowInvalidCertificates}");

        return client;
    }

    private async Task<AsyncFtpClient> GetOrCreateClientAsync(
        FtpConfiguration configuration, CancellationToken cancellationToken)
    {
        await _clientLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_client != null && _client.IsConnected)
            {
                return _client;
            }

            if (_client != null)
            {
                await _client.DisposeAsync().ConfigureAwait(false);
                _client = null;
            }

            _client = CreateClient(configuration);
            await _client.Connect(cancellationToken).ConfigureAwait(false);

            if (!_client.IsConnected)
            {
                throw new InvalidOperationException("Failed to connect FTP client");
            }

            return _client;
        }
        finally
        {
            _clientLock.Release();
        }
    }

    private void ResetClient()
    {
        try
        {
            _client?.Dispose();
        }
        catch
        {
            // ignore cleanup errors
        }

        _client = null;
    }

    private static void CleanupIncompleteFile(string localPath)
    {
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
    }

    public void Dispose()
    {
        if (_disposed) return;

        _client?.Dispose();
        _client = null;
        _clientLock.Dispose();
        _disposed = true;
    }
}
