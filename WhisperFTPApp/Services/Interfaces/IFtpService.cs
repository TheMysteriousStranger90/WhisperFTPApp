using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WhisperFTPApp.Configurations;
using WhisperFTPApp.Models;

namespace WhisperFTPApp.Services.Interfaces;

public interface IFtpService
{
    Task<bool> ConnectAsync(FtpConfiguration configuration);
    Task DisconnectAsync();
    Task<IEnumerable<FileSystemItem>> ListDirectoryAsync(FtpConfiguration configuration, string path = "/");
    Task UploadFileAsync(FtpConfiguration configuration, string localPath, string remotePath, IProgress<double> progress);
    Task DownloadFileAsync(FtpConfiguration configuration, string remotePath, string localPath, IProgress<double> progress);
    Task DeleteFileAsync(FtpConfiguration configuration, string remotePath);
    Task DeleteDirectoryAsync(FtpConfiguration configuration, string path);
}