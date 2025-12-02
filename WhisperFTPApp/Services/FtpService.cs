using System.Globalization;
using System.Net;
using System.Net.Security;
using WhisperFTPApp.Configurations;
using WhisperFTPApp.Logger;
using WhisperFTPApp.Models;
using WhisperFTPApp.Services.Interfaces;

namespace WhisperFTPApp.Services;

internal sealed class FtpService : IFtpService
{
    private FtpWebRequest? _currentRequest;
    private const int MaxRetries = 3;
    private const int BaseDelay = 2000;
    private const int ConnectionTimeout = 15000;
    private readonly Dictionary<string, ServicePoint> _servicePoints = new();

    #region Connection Management

    [Obsolete("Obsolete")]
    public async Task<bool> ConnectAsync(FtpConfiguration configuration, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        StaticFileLogger.LogInformation($"Attempting to connect to {configuration.FtpAddress}");

        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                StaticFileLogger.LogInformation($"Connection attempt {attempt} of {MaxRetries}");

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(ConnectionTimeout);

                var connected = await SendRequestAsync(configuration).ConfigureAwait(false);

                if (connected)
                {
                    OptimizeServicePoint(configuration);
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
            catch (WebException ex)
            {
                StaticFileLogger.LogError($"Connection attempt {attempt} failed: {ex.Message}");
                StaticFileLogger.LogError($"Status: {ex.Status}");

                if (IsAuthenticationError(ex))
                {
                    StaticFileLogger.LogError("Authentication failed - stopping retries");
                    return false;
                }

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
                StaticFileLogger.LogError($"Unexpected error during connection: {ex.Message}");
                if (attempt >= MaxRetries)
                {
                    return false;
                }

                await Task.Delay(BaseDelay * attempt, cancellationToken).ConfigureAwait(false);
            }
        }

        StaticFileLogger.LogError("All connection attempts failed");
        return false;
    }

    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        _currentRequest?.Abort();
        _currentRequest = null;
        _servicePoints.Clear();
        StaticFileLogger.LogInformation("Disconnected from FTP server");
        return Task.CompletedTask;
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
            var items = new List<FileSystemItem>();
            var uri = new Uri($"{configuration.FtpAddress.TrimEnd('/')}/{path.TrimStart('/')}");
            var request = CreateRequest(uri, configuration);
            request.Method = WebRequestMethods.Ftp.ListDirectoryDetails;

            using var response = (FtpWebResponse)await request.GetResponseAsync().ConfigureAwait(false);
            using var stream = response.GetResponseStream();
            using var streamReader = new StreamReader(stream);

            string? line;
            while ((line = await streamReader.ReadLineAsync(cancellationToken).ConfigureAwait(false)) != null)
            {
                var item = ParseFtpListItem(line, path);
                if (item != null)
                {
                    items.Add(item);
                }
            }

            StaticFileLogger.LogInformation($"Listed {items.Count} items in directory {path}");
            return items;
        }
        catch (WebException ex)
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
            var uri = new Uri($"{configuration.FtpAddress.TrimEnd('/')}/{remotePath.TrimStart('/')}");
            var request = CreateRequest(uri, configuration);
            request.Method = WebRequestMethods.Ftp.MakeDirectory;

            using var response = (FtpWebResponse)await request.GetResponseAsync().ConfigureAwait(false);
            StaticFileLogger.LogInformation($"Directory created: {remotePath} - {response.StatusDescription}");
        }
        catch (WebException ex)
        {
            if (ex.Response is FtpWebResponse ftpResponse)
            {
                StaticFileLogger.LogError(
                    $"Failed to create directory {remotePath}: {ftpResponse.StatusCode} - {ftpResponse.StatusDescription}");
            }

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
            var items = await ListDirectoryAsync(configuration, path, cancellationToken).ConfigureAwait(false);

            foreach (var item in items)
            {
                if (item.Name == "." || item.Name == "..")
                    continue;

                if (item.IsDirectory)
                {
                    await DeleteDirectoryAsync(configuration, item.FullPath, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    await DeleteFileAsync(configuration, item.FullPath, cancellationToken).ConfigureAwait(false);
                }
            }

            var uri = new Uri($"{configuration.FtpAddress.TrimEnd('/')}/{path.TrimStart('/')}");
            var request = CreateRequest(uri, configuration);
            request.Method = WebRequestMethods.Ftp.RemoveDirectory;

            using var response = (FtpWebResponse)await request.GetResponseAsync().ConfigureAwait(false);
            StaticFileLogger.LogInformation($"Directory deleted successfully: {path} - {response.StatusDescription}");
        }
        catch (WebException ex)
        {
            if (ex.Response is FtpWebResponse ftpResponse)
            {
                StaticFileLogger.LogError(
                    $"Delete directory failed {path}: {ftpResponse.StatusCode} - {ftpResponse.StatusDescription}");
            }
            else
            {
                StaticFileLogger.LogError($"Delete directory failed {path}: {ex.Message}");
            }

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
            var uri = new Uri($"{configuration.FtpAddress.TrimEnd('/')}/{remotePath.TrimStart('/')}");
            var request = CreateRequest(uri, configuration);
            request.Method = WebRequestMethods.Ftp.ListDirectory;

            using var response = (FtpWebResponse)await request.GetResponseAsync().ConfigureAwait(false);
            return true;
        }
        catch (WebException ex) when (ex.Response is FtpWebResponse ftpResponse &&
                                      ftpResponse.StatusCode == FtpStatusCode.ActionNotTakenFileUnavailable)
        {
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
            var uri = new Uri($"{configuration.FtpAddress.TrimEnd('/')}/");
            var request = CreateRequest(uri, configuration);
            request.Method = WebRequestMethods.Ftp.PrintWorkingDirectory;

            using var response = (FtpWebResponse)await request.GetResponseAsync().ConfigureAwait(false);
            using var stream = response.GetResponseStream();
            using var reader = new StreamReader(stream);

            var directory = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            StaticFileLogger.LogInformation($"Current working directory: {directory}");
            return directory.Trim();
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
            var uri = new Uri($"{configuration.FtpAddress.TrimEnd('/')}/{remotePath.TrimStart('/')}");
            var request = CreateRequestForLargeFile(uri, configuration);
            request.Method = WebRequestMethods.Ftp.UploadFile;

            var fileStream = new FileStream(
                localPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 262144,
                useAsync: true);

            await using (fileStream.ConfigureAwait(false))
            {
                var ftpStream = await request.GetRequestStreamAsync().ConfigureAwait(false);
                await using (ftpStream.ConfigureAwait(false))
                {
                    var buffer = new byte[131072];
                    long totalBytes = fileStream.Length;
                    long bytesRead = 0;
                    int count;
                    int progressUpdateCounter = 0;
                    var lastProgressTime = DateTime.UtcNow;

                    StaticFileLogger.LogInformation(
                        $"File size: {totalBytes} bytes ({totalBytes / 1024.0 / 1024.0:F2} MB)");

                    while ((count = await fileStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
                    {
                        await ftpStream.WriteAsync(buffer.AsMemory(0, count), cancellationToken).ConfigureAwait(false);
                        bytesRead += count;

                        if (++progressUpdateCounter >= 10 ||
                            (DateTime.UtcNow - lastProgressTime).TotalMilliseconds > 500)
                        {
                            var percentage = (double)bytesRead / totalBytes * 100;
                            progress?.Report(percentage);
                            progressUpdateCounter = 0;
                            lastProgressTime = DateTime.UtcNow;

                            if (totalBytes > 10 * 1024 * 1024)
                            {
                                StaticFileLogger.LogInformation(
                                    $"Upload progress: {bytesRead / 1024.0 / 1024.0:F2}/{totalBytes / 1024.0 / 1024.0:F2} MB ({percentage:F1}%)");
                            }
                        }
                    }

                    progress?.Report(100);
                }
            }

            StaticFileLogger.LogInformation($"Upload completed: {remotePath}");
        }
        catch (WebException ex)
        {
            StaticFileLogger.LogError($"Upload failed {remotePath}: {ex.Message}");
            StaticFileLogger.LogError($"Status: {ex.Status}");
            throw;
        }
        catch (Exception ex)
        {
            StaticFileLogger.LogError($"Upload failed with unexpected error: {ex.Message}");
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
            var fileInfo = new FileInfo(localPath);
            long remoteSize = 0;

            try
            {
                remoteSize = await GetFileSizeAsync(configuration, remotePath, cancellationToken).ConfigureAwait(false);
                StaticFileLogger.LogInformation($"Remote file exists with size: {remoteSize} bytes");
            }
            catch
            {
                StaticFileLogger.LogInformation("Remote file does not exist, starting fresh upload");
            }

            var uri = new Uri($"{configuration.FtpAddress.TrimEnd('/')}/{remotePath.TrimStart('/')}");
            var request = CreateRequest(uri, configuration);

            if (remoteSize > 0 && remoteSize < fileInfo.Length)
            {
                request.Method = WebRequestMethods.Ftp.AppendFile;
                request.ContentOffset = remoteSize;
                StaticFileLogger.LogInformation($"Resuming upload from byte {remoteSize}");
            }
            else if (remoteSize >= fileInfo.Length)
            {
                StaticFileLogger.LogInformation("Remote file is complete, skipping upload");
                progress?.Report(100);
                return;
            }
            else
            {
                request.Method = WebRequestMethods.Ftp.UploadFile;
            }

            using var fileStream = new FileStream(
                localPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 262144,
                useAsync: true);

            if (remoteSize > 0)
            {
                fileStream.Seek(remoteSize, SeekOrigin.Begin);
            }

            using var ftpStream = await request.GetRequestStreamAsync().ConfigureAwait(false);

            var buffer = new byte[131072];
            long totalBytes = fileStream.Length;
            long bytesRead = remoteSize;
            int count;
            int progressUpdateCounter = 0;

            while ((count = await fileStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                await ftpStream.WriteAsync(buffer.AsMemory(0, count), cancellationToken).ConfigureAwait(false);
                bytesRead += count;

                if (++progressUpdateCounter >= 10)
                {
                    progress?.Report((double)bytesRead / totalBytes * 100);
                    progressUpdateCounter = 0;
                }
            }

            progress?.Report(100);
            StaticFileLogger.LogInformation($"Upload with resume completed: {remotePath}");
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
            var uri = new Uri($"{configuration.FtpAddress.TrimEnd('/')}/{remotePath.TrimStart('/')}");
            var request = CreateRequestForLargeFile(uri, configuration);
            request.Method = WebRequestMethods.Ftp.DownloadFile;

            using var response = (FtpWebResponse)await request.GetResponseAsync().ConfigureAwait(false);
            using var ftpStream = response.GetResponseStream();

            var fileStream = new FileStream(
                localPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 262144,
                useAsync: true);

            await using (fileStream.ConfigureAwait(false))
            {
                var buffer = new byte[131072];
                long totalBytes = response.ContentLength;
                long bytesRead = 0;
                int count;
                int progressUpdateCounter = 0;
                var lastProgressTime = DateTime.UtcNow;

                StaticFileLogger.LogInformation(
                    $"File size: {totalBytes} bytes ({totalBytes / 1024.0 / 1024.0:F2} MB)");

                while ((count = await ftpStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, count), cancellationToken).ConfigureAwait(false);
                    bytesRead += count;

                    if (++progressUpdateCounter >= 10 || (DateTime.UtcNow - lastProgressTime).TotalMilliseconds > 500)
                    {
                        var percentage = (double)bytesRead / totalBytes * 100;
                        progress?.Report(percentage);
                        progressUpdateCounter = 0;
                        lastProgressTime = DateTime.UtcNow;

                        if (totalBytes > 10 * 1024 * 1024)
                        {
                            StaticFileLogger.LogInformation(
                                $"Download progress: {bytesRead / 1024.0 / 1024.0:F2}/{totalBytes / 1024.0 / 1024.0:F2} MB ({percentage:F1}%)");
                        }
                    }
                }

                progress?.Report(100);
            }

            StaticFileLogger.LogInformation($"Download completed: {localPath}");
        }
        catch (WebException ex)
        {
            StaticFileLogger.LogError($"Download failed {remotePath}: {ex.Message}");
            StaticFileLogger.LogError($"Status: {ex.Status}");

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
        catch (Exception ex)
        {
            StaticFileLogger.LogError($"Download failed with unexpected error: {ex.Message}");

            if (File.Exists(localPath))
            {
                try
                {
                    File.Delete(localPath);
                }
                catch
                {
                    // Ignored
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
            var uri = new Uri($"{configuration.FtpAddress.TrimEnd('/')}/{remotePath.TrimStart('/')}");
            var request = CreateRequest(uri, configuration);
            request.Method = WebRequestMethods.Ftp.DeleteFile;

            using var response = (FtpWebResponse)await request.GetResponseAsync().ConfigureAwait(false);
            StaticFileLogger.LogInformation($"File deleted successfully: {remotePath} - {response.StatusDescription}");
        }
        catch (WebException ex)
        {
            if (ex.Response is FtpWebResponse ftpResponse)
            {
                StaticFileLogger.LogError(
                    $"Delete file failed {remotePath}: {ftpResponse.StatusCode} - {ftpResponse.StatusDescription}");
            }
            else
            {
                StaticFileLogger.LogError($"Delete file failed {remotePath}: {ex.Message}");
            }

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
            var uri = new Uri($"{configuration.FtpAddress.TrimEnd('/')}/{currentName.TrimStart('/')}");
            var request = CreateRequest(uri, configuration);
            request.Method = WebRequestMethods.Ftp.Rename;
            request.RenameTo = newName;

            using var response = (FtpWebResponse)await request.GetResponseAsync().ConfigureAwait(false);
            StaticFileLogger.LogInformation($"Renamed successfully: {response.StatusDescription}");
        }
        catch (WebException ex)
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
            var uri = new Uri($"{configuration.FtpAddress.TrimEnd('/')}/{remotePath.TrimStart('/')}");
            var request = CreateRequest(uri, configuration);
            request.Method = WebRequestMethods.Ftp.GetFileSize;

            using var response = (FtpWebResponse)await request.GetResponseAsync().ConfigureAwait(false);
            return true;
        }
        catch (WebException ex) when (ex.Response is FtpWebResponse ftpResponse &&
                                      ftpResponse.StatusCode == FtpStatusCode.ActionNotTakenFileUnavailable)
        {
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
            var uri = new Uri($"{configuration.FtpAddress.TrimEnd('/')}/{remotePath.TrimStart('/')}");
            var request = CreateRequest(uri, configuration);
            request.Method = WebRequestMethods.Ftp.GetFileSize;

            using var response = (FtpWebResponse)await request.GetResponseAsync().ConfigureAwait(false);
            return response.ContentLength;
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
            var uri = new Uri($"{configuration.FtpAddress.TrimEnd('/')}/{remotePath.TrimStart('/')}");
            var request = CreateRequest(uri, configuration);
            request.Method = WebRequestMethods.Ftp.GetDateTimestamp;

            using var response = (FtpWebResponse)await request.GetResponseAsync().ConfigureAwait(false);
            return response.LastModified;
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
            var uri = new Uri($"{configuration.FtpAddress.TrimEnd('/')}/{remotePath.TrimStart('/')}");
            var request = CreateRequest(uri, configuration);
            request.Method = WebRequestMethods.Ftp.GetFileSize;

            using var response = (FtpWebResponse)await request.GetResponseAsync().ConfigureAwait(false);

            var fileItem = new FileSystemItem
            {
                Name = Path.GetFileName(remotePath),
                FullPath = remotePath,
                IsDirectory = false,
                Size = response.ContentLength,
                Modified = response.LastModified,
                Type = Path.GetExtension(remotePath)
            };

            StaticFileLogger.LogInformation(
                $"File details retrieved - Name: {fileItem.Name}, Size: {fileItem.Size}, Modified: {fileItem.Modified}");
            StaticFileLogger.LogInformation($"Response URI: {response.ResponseUri}");

            if (!string.IsNullOrEmpty(response.WelcomeMessage))
                StaticFileLogger.LogInformation($"Welcome Message: {response.WelcomeMessage}");

            if (!string.IsNullOrEmpty(response.BannerMessage))
                StaticFileLogger.LogInformation($"Banner Message: {response.BannerMessage}");

            return fileItem;
        }
        catch (Exception ex)
        {
            StaticFileLogger.LogError($"Failed to get file details: {ex.Message}");
            throw;
        }
    }

    #endregion

    #region Security

    public async Task<bool> TestSecureConnectionAsync(
        FtpConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        StaticFileLogger.LogInformation("Testing SSL/TLS connection");

        try
        {
            var uri = new Uri($"{configuration.FtpAddress.TrimEnd('/')}/");
            var request = CreateRequest(uri, configuration);
            request.Method = WebRequestMethods.Ftp.ListDirectory;
            request.EnableSsl = true;

            using var response = (FtpWebResponse)await request.GetResponseAsync().ConfigureAwait(false);
            StaticFileLogger.LogInformation($"SSL connection successful - Status: {response.StatusDescription}");
            return true;
        }
        catch (Exception ex)
        {
            StaticFileLogger.LogError($"SSL connection failed: {ex.Message}");
            return false;
        }
    }

    #endregion

    #region Helper Methods

    private FtpWebRequest CreateRequest(Uri uri, FtpConfiguration configuration)
    {
#pragma warning disable SYSLIB0014
        var request = (FtpWebRequest)WebRequest.Create(uri);
        request.Credentials = new NetworkCredential(configuration.Username, configuration.Password);
        request.EnableSsl = configuration.EnableSsl;
        if (configuration.EnableSsl)
        {
            ServicePointManager.ServerCertificateValidationCallback =
                (sender, certificate, chain, sslPolicyErrors) =>
                {
                    if (sslPolicyErrors == SslPolicyErrors.None)
                        return true;
                    StaticFileLogger.LogWarning($"SSL Certificate Error: {sslPolicyErrors}");
                    return false;
                };
        }

        request.UsePassive = configuration.UsePassive;
        request.UseBinary = true;
        request.KeepAlive = true;
        request.Timeout = configuration.Timeout > 0 ? configuration.Timeout : ConnectionTimeout;
        request.ReadWriteTimeout = configuration.ReadWriteTimeout > 0
            ? configuration.ReadWriteTimeout
            : ConnectionTimeout;

        _currentRequest = request;
        return request;
#pragma warning restore SYSLIB0014
    }

    [Obsolete("Obsolete")]
    private void OptimizeServicePoint(FtpConfiguration configuration)
    {
        var uriBuilder = new UriBuilder(configuration.FtpAddress)
        {
            Port = configuration.Port
        };

        var servicePoint = ServicePointManager.FindServicePoint(uriBuilder.Uri);

        servicePoint.ConnectionLimit = 10;
        servicePoint.MaxIdleTime = 30000;
        servicePoint.UseNagleAlgorithm = false;
        servicePoint.Expect100Continue = false;

        _servicePoints[configuration.FtpAddress] = servicePoint;

        StaticFileLogger.LogInformation(
            $"ServicePoint optimized - ConnectionLimit: {servicePoint.ConnectionLimit}, MaxIdleTime: {servicePoint.MaxIdleTime}ms");
    }

    [Obsolete("Obsolete")]
    private static async Task<bool> SendRequestAsync(FtpConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var uriBuilder = new UriBuilder(configuration.FtpAddress)
        {
            Port = configuration.Port
        };

        var request = (FtpWebRequest)WebRequest.Create(uriBuilder.Uri);
        request.Method = WebRequestMethods.Ftp.ListDirectory;
        request.Credentials = new NetworkCredential(configuration.Username, configuration.Password);
        request.Timeout = ConnectionTimeout;
        request.ReadWriteTimeout = ConnectionTimeout;
        request.KeepAlive = true;
        request.UsePassive = true;
        request.UseBinary = true;

        try
        {
            using var response = (FtpWebResponse)await request.GetResponseAsync().ConfigureAwait(false);
            StaticFileLogger.LogInformation($"FTP Response: {response.StatusCode} - {response.StatusDescription}");

            return response.StatusCode == FtpStatusCode.OpeningData ||
                   response.StatusCode == FtpStatusCode.DataAlreadyOpen ||
                   response.StatusCode == FtpStatusCode.CommandOK;
        }
        catch (WebException ex)
        {
            if (ex.Response is FtpWebResponse ftpResponse)
            {
                StaticFileLogger.LogError($"FTP Error: {ftpResponse.StatusCode} - {ftpResponse.StatusDescription}");
            }

            throw;
        }
    }

    private static bool IsAuthenticationError(WebException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (exception.Status == WebExceptionStatus.ProtocolError &&
            exception.Response is FtpWebResponse response)
        {
            return response.StatusCode == FtpStatusCode.NotLoggedIn ||
                   response.StatusCode == FtpStatusCode.AccountNeeded ||
                   response.StatusCode == FtpStatusCode.NeedLoginAccount;
        }

        return false;
    }

    private static FileSystemItem? ParseFtpListItem(string line, string currentPath)
    {
        ArgumentNullException.ThrowIfNull(line);
        ArgumentNullException.ThrowIfNull(currentPath);

        try
        {
            var parts = line.Split([' '], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 4) return null;

            DateTime modifiedDate;
            var dateString = $"{parts[^4]} {parts[^3]} {parts[^2]}";

            string[] formats =
            [
                "MMM dd yyyy",
                "MMM dd HH:mm",
                "MMM dd hh:mm",
                "yyyy-MM-dd HH:mm",
                "dd MMM yyyy",
                "dd MMM HH:mm"
            ];

            if (!DateTime.TryParseExact(dateString,
                    formats,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out modifiedDate))
            {
                var monthDay = $"{parts[^4]} {parts[^3]}";
                if (DateTime.TryParseExact(monthDay,
                        ["MMM dd", "dd MMM"],
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out var partialDate))
                {
                    modifiedDate = new DateTime(DateTime.Now.Year, partialDate.Month, partialDate.Day, 0, 0, 0,
                        DateTimeKind.Local);
                }
                else
                {
                    modifiedDate = DateTime.Now;
                }
            }

            return new FileSystemItem
            {
                Name = parts[^1],
                FullPath = $"{currentPath.TrimEnd('/')}/{parts[^1]}",
                IsDirectory = line.StartsWith('d'),
                Size = long.TryParse(parts[^5], out var size) ? size : 0,
                Modified = modifiedDate
            };
        }
        catch (FormatException)
        {
            return null;
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    private FtpWebRequest CreateRequestForLargeFile(Uri uri, FtpConfiguration configuration)
    {
#pragma warning disable SYSLIB0014
        var request = (FtpWebRequest)WebRequest.Create(uri);
        request.Credentials = new NetworkCredential(configuration.Username, configuration.Password);

        request.Timeout = 30000;

        request.ReadWriteTimeout = configuration.ReadWriteTimeout > 0
            ? configuration.ReadWriteTimeout
            : 300000;

        request.KeepAlive = true;
        request.UsePassive = configuration.UsePassive;
        request.UseBinary = true;

        _currentRequest = request;

        StaticFileLogger.LogInformation(
            $"Request created - Timeout: {request.Timeout}ms, ReadWriteTimeout: {request.ReadWriteTimeout}ms");

        return request;
#pragma warning restore SYSLIB0014
    }

    #endregion
}
