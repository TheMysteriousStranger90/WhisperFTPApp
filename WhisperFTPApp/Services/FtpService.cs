using System.Globalization;
using System.Net;
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
                StaticFileLogger.LogInformation($"Waiting {delay}ms before retry");
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

#pragma warning disable SYSLIB0014
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
        request.KeepAlive = false;
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

    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        _currentRequest?.Abort();
        _currentRequest = null;
        StaticFileLogger.LogInformation("Disconnected from FTP server");
        return Task.CompletedTask;
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
            var request = (FtpWebRequest)WebRequest.Create(uri);
            request.Method = WebRequestMethods.Ftp.ListDirectoryDetails;
            request.Credentials = new NetworkCredential(configuration.Username, configuration.Password);
            request.Timeout = ConnectionTimeout;
            request.ReadWriteTimeout = ConnectionTimeout;
            request.KeepAlive = false;
            request.UsePassive = true;

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
            var request = (FtpWebRequest)WebRequest.Create(uri);
            request.Method = WebRequestMethods.Ftp.UploadFile;
            request.Credentials = new NetworkCredential(configuration.Username, configuration.Password);
            request.Timeout = ConnectionTimeout;
            request.ReadWriteTimeout = ConnectionTimeout;
            request.KeepAlive = true;
            request.UsePassive = true;
            request.UseBinary = true;

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
                }
            }

            StaticFileLogger.LogInformation($"Upload completed: {remotePath}");
        }
        catch (WebException ex)
        {
            StaticFileLogger.LogError($"Upload failed {remotePath}: {ex.Message}");
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
            var request = (FtpWebRequest)WebRequest.Create(uri);
            request.Method = WebRequestMethods.Ftp.DownloadFile;
            request.Credentials = new NetworkCredential(configuration.Username, configuration.Password);
            request.Timeout = ConnectionTimeout;
            request.ReadWriteTimeout = ConnectionTimeout;
            request.KeepAlive = true;
            request.UsePassive = true;
            request.UseBinary = true;

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

                while ((count = await ftpStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, count), cancellationToken).ConfigureAwait(false);
                    bytesRead += count;

                    if (++progressUpdateCounter >= 10)
                    {
                        progress?.Report((double)bytesRead / totalBytes * 100);
                        progressUpdateCounter = 0;
                    }
                }

                progress?.Report(100);
            }

            StaticFileLogger.LogInformation($"Download completed: {localPath}");
        }
        catch (WebException ex)
        {
            StaticFileLogger.LogError($"Download failed {remotePath}: {ex.Message}");
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
            var request = (FtpWebRequest)WebRequest.Create(uri);
            request.Method = WebRequestMethods.Ftp.DeleteFile;
            request.Credentials = new NetworkCredential(configuration.Username, configuration.Password);
            request.Timeout = ConnectionTimeout;
            request.ReadWriteTimeout = ConnectionTimeout;
            request.KeepAlive = false;
            request.UsePassive = true;

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
            var request = (FtpWebRequest)WebRequest.Create(uri);
            request.Method = WebRequestMethods.Ftp.RemoveDirectory;
            request.Credentials = new NetworkCredential(configuration.Username, configuration.Password);
            request.Timeout = ConnectionTimeout;
            request.ReadWriteTimeout = ConnectionTimeout;
            request.KeepAlive = false;
            request.UsePassive = true;

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
#pragma warning restore SYSLIB0014

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
}
