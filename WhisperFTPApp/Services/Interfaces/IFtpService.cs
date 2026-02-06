using WhisperFTPApp.Configurations;
using WhisperFTPApp.Models;

namespace WhisperFTPApp.Services.Interfaces;

public interface IFtpService
{
    Task<bool> ConnectAsync(FtpConfiguration configuration, CancellationToken cancellationToken = default);
    Task DisconnectAsync(CancellationToken cancellationToken = default);
    Task<bool> TestSecureConnectionAsync(FtpConfiguration configuration, CancellationToken cancellationToken = default);

    Task<IEnumerable<FileSystemItem>> ListDirectoryAsync(FtpConfiguration configuration, string path = "/",
        CancellationToken cancellationToken = default);

    Task CreateDirectoryAsync(FtpConfiguration configuration, string remotePath,
        CancellationToken cancellationToken = default);

    Task DeleteDirectoryAsync(FtpConfiguration configuration, string path,
        CancellationToken cancellationToken = default);

    Task<bool> DirectoryExistsAsync(FtpConfiguration configuration, string remotePath,
        CancellationToken cancellationToken = default);

    Task<string> GetWorkingDirectoryAsync(FtpConfiguration configuration,
        CancellationToken cancellationToken = default);

    Task UploadFileAsync(FtpConfiguration configuration, string localPath, string remotePath,
        IProgress<double>? progress = null, CancellationToken cancellationToken = default);

    Task UploadFileWithResumeAsync(FtpConfiguration configuration, string localPath, string remotePath,
        IProgress<double>? progress = null, CancellationToken cancellationToken = default);

    Task DownloadFileAsync(FtpConfiguration configuration, string remotePath, string localPath,
        IProgress<double>? progress = null, CancellationToken cancellationToken = default);

    Task DeleteFileAsync(FtpConfiguration configuration, string remotePath,
        CancellationToken cancellationToken = default);

    Task RenameAsync(FtpConfiguration configuration, string currentName, string newName,
        CancellationToken cancellationToken = default);

    Task<bool> FileExistsAsync(FtpConfiguration configuration, string remotePath,
        CancellationToken cancellationToken = default);

    Task<long> GetFileSizeAsync(FtpConfiguration configuration, string remotePath,
        CancellationToken cancellationToken = default);

    Task<DateTime> GetFileModifiedTimeAsync(FtpConfiguration configuration, string remotePath,
        CancellationToken cancellationToken = default);

    Task<FileSystemItem> GetFileDetailsAsync(FtpConfiguration configuration, string remotePath,
        CancellationToken cancellationToken = default);
}
