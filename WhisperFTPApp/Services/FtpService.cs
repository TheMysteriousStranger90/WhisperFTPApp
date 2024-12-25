using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using WhisperFTPApp.Configurations;
using WhisperFTPApp.Models;
using WhisperFTPApp.Services.Interfaces;
using System.Globalization;
using WhisperFTPApp.Logger;

namespace WhisperFTPApp.Services;

public class FtpService : IFtpService
{
    private FtpWebRequest _currentRequest;
    private const int MaxRetries = 3;
    private const int BaseDelay = 1000;

    public async Task<bool> ConnectAsync(FtpConfiguration configuration)
    {
        StaticFileLogger.LogInformation($"Attempting to connect to {configuration.FtpAddress}");
        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                StaticFileLogger.LogInformation($"Connection attempt {attempt} of {MaxRetries}");
                var connectTask = SendRequest(configuration);
                var timeoutTask = Task.Delay(configuration.Timeout);
                var completedTask = await Task.WhenAny(connectTask, timeoutTask);

                if (completedTask == connectTask)
                {
                    return await connectTask;
                }

                StaticFileLogger.LogError($"Connection attempt {attempt} timed out");
                if (attempt < MaxRetries)
                {
                    int delay = BaseDelay * (int)Math.Pow(2, attempt - 1);
                    StaticFileLogger.LogInformation($"Waiting {delay}ms before retry");
                    await Task.Delay(delay);
                }
            }
            catch (WebException ex)
            {
                StaticFileLogger.LogError($"Connection attempt {attempt} failed: {ex.Message}");
                if (attempt == MaxRetries)
                {
                    return IsAuthenticationError(ex);
                }

                int delay = BaseDelay * (int)Math.Pow(2, attempt - 1);
                await Task.Delay(delay);
            }
        }

        StaticFileLogger.LogError("All connection attempts failed");
        return false;
    }

    private async Task<bool> SendRequest(FtpConfiguration configuration)
    {
        var uriBuilder = new UriBuilder(configuration.FtpAddress)
        {
            Port = configuration.Port
        };

        _currentRequest = (FtpWebRequest)WebRequest.Create(uriBuilder.Uri);
        _currentRequest.Method = WebRequestMethods.Ftp.ListDirectory;
        _currentRequest.Credentials = new NetworkCredential(configuration.Username, configuration.Password);
        _currentRequest.Timeout = configuration.Timeout;
        _currentRequest.KeepAlive = false;

        using (FtpWebResponse response = (FtpWebResponse)await _currentRequest.GetResponseAsync())
        {
            return response.StatusCode == FtpStatusCode.OpeningData ||
                   response.StatusCode == FtpStatusCode.AccountNeeded;
        }
    }

    public async Task DisconnectAsync()
    {
        if (_currentRequest != null)
        {
            _currentRequest.Abort();
            _currentRequest = null;
        }
    }

    private static async Task<bool> DetermineConnectionStatusAsync(Task<bool> sendRequestTask, Task timeoutTask)
    {
        var completedTask = await Task.WhenAny(sendRequestTask, timeoutTask);
        return completedTask == sendRequestTask && await AttemptConnectionAsync(sendRequestTask);
    }

    private static async Task<bool> AttemptConnectionAsync(Task<bool> sendRequestTask)
    {
        try
        {
            return await sendRequestTask;
        }
        catch (WebException)
        {
            return false;
        }
    }

    private bool IsAuthenticationError(WebException exception)
    {
        if (exception.Status == WebExceptionStatus.ProtocolError)
        {
            FtpWebResponse response = exception.Response as FtpWebResponse;
            if (response?.StatusCode == FtpStatusCode.NotLoggedIn)
            {
                return true;
            }
        }

        return false;
    }

    public async Task<IEnumerable<FileSystemItem>> ListDirectoryAsync(FtpConfiguration configuration, string path = "/")
    {
        StaticFileLogger.LogInformation($"Listing directory: {path}");
        try
        {
            var items = new List<FileSystemItem>();
            var request =
                (FtpWebRequest)WebRequest.Create($"{configuration.FtpAddress.TrimEnd('/')}/{path.TrimStart('/')}");
            request.Method = WebRequestMethods.Ftp.ListDirectoryDetails;
            request.Credentials = new NetworkCredential(configuration.Username, configuration.Password);

            using var response = (FtpWebResponse)await request.GetResponseAsync();
            using var streamReader = new StreamReader(response.GetResponseStream());
            string line;

            while ((line = await streamReader.ReadLineAsync()) != null)
            {
                var item = ParseFtpListItem(line, path);
                if (item != null)
                    items.Add(item);
            }

            StaticFileLogger.LogInformation($"Listed {items.Count} items in directory {path}");
            return items;
        }
        catch (Exception ex)
        {
            StaticFileLogger.LogError($"Failed to list directory {path}: {ex.Message}");
            throw;
        }
    }

    public async Task UploadFileAsync(FtpConfiguration configuration, string localPath, string remotePath,
        IProgress<double> progress)
    {
        StaticFileLogger.LogInformation($"Starting upload: {localPath} -> {remotePath}");
        try
        {
            var request =
                (FtpWebRequest)WebRequest.Create(
                    $"{configuration.FtpAddress.TrimEnd('/')}/{remotePath.TrimStart('/')}");
            request.Method = WebRequestMethods.Ftp.UploadFile;
            request.Credentials = new NetworkCredential(configuration.Username, configuration.Password);

            using var fileStream = File.OpenRead(localPath);
            using var ftpStream = await request.GetRequestStreamAsync();

            var buffer = new byte[8192];
            long totalBytes = fileStream.Length;
            long bytesRead = 0;
            int count;

            while ((count = await fileStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await ftpStream.WriteAsync(buffer, 0, count);
                bytesRead += count;
                progress?.Report((double)bytesRead / totalBytes * 100);
            }

            StaticFileLogger.LogInformation($"Upload completed: {remotePath}");
        }
        catch (Exception ex)
        {
            StaticFileLogger.LogError($"Upload failed {remotePath}: {ex.Message}");
            throw;
        }
    }

    public async Task DownloadFileAsync(FtpConfiguration configuration, string remotePath, string localPath,
        IProgress<double> progress)
    {
        StaticFileLogger.LogInformation($"Starting download: {remotePath} -> {localPath}");
        try
        {
            var request =
                (FtpWebRequest)WebRequest.Create(
                    $"{configuration.FtpAddress.TrimEnd('/')}/{remotePath.TrimStart('/')}");
            request.Method = WebRequestMethods.Ftp.DownloadFile;
            request.Credentials = new NetworkCredential(configuration.Username, configuration.Password);

            using var response = (FtpWebResponse)await request.GetResponseAsync();
            using var ftpStream = response.GetResponseStream();
            using var fileStream = File.Create(localPath);

            var buffer = new byte[8192];
            long totalBytes = response.ContentLength;
            long bytesRead = 0;
            int count;

            while ((count = await ftpStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, count);
                bytesRead += count;
                progress?.Report((double)bytesRead / totalBytes * 100);
            }

            StaticFileLogger.LogInformation($"Download completed: {localPath}");
        }
        catch (Exception ex)
        {
            StaticFileLogger.LogError($"Download failed {remotePath}: {ex.Message}");
            throw;
        }
    }

    public async Task DeleteFileAsync(FtpConfiguration configuration, string remotePath)
    {
        StaticFileLogger.LogInformation($"Deleting file: {remotePath}");
        try
        {
            var request =
                (FtpWebRequest)WebRequest.Create(
                    $"{configuration.FtpAddress.TrimEnd('/')}/{remotePath.TrimStart('/')}");
            request.Method = WebRequestMethods.Ftp.DeleteFile;
            request.Credentials = new NetworkCredential(configuration.Username, configuration.Password);

            using var response = (FtpWebResponse)await request.GetResponseAsync();
            StaticFileLogger.LogInformation($"File deleted: {remotePath}");
        }
        catch (Exception ex)
        {
            StaticFileLogger.LogError($"Delete failed {remotePath}: {ex.Message}");
            throw;
        }
    }


    private FileSystemItem ParseFtpListItem(string line, string currentPath)
    {
        try
        {
            var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 4) return null;

            DateTime modifiedDate;
            var dateString = $"{parts[^4]} {parts[^3]} {parts[^2]}";

            var formats = new[]
            {
                "MMM dd yyyy",
                "MMM dd HH:mm",
                "MMM dd hh:mm",
                "yyyy-MM-dd HH:mm",
                "dd MMM yyyy",
                "dd MMM HH:mm"
            };

            if (!DateTime.TryParseExact(dateString,
                    formats,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out modifiedDate))
            {
                var monthDay = $"{parts[^4]} {parts[^3]}";
                if (DateTime.TryParseExact(monthDay,
                        new[] { "MMM dd", "dd MMM" },
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out var partialDate))
                {
                    modifiedDate = new DateTime(DateTime.Now.Year, partialDate.Month, partialDate.Day);
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
        catch (Exception ex)
        {
            return null;
        }
    }
}