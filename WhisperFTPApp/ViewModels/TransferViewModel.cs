using System.Net;
using System.Reactive;
using ReactiveUI;
using WhisperFTPApp.Configurations;
using WhisperFTPApp.Events;
using WhisperFTPApp.Logger;
using WhisperFTPApp.Models;
using WhisperFTPApp.Models.Transfer;
using WhisperFTPApp.Services.Interfaces;

namespace WhisperFTPApp.ViewModels;

public sealed class TransferViewModel : ReactiveObject, IDisposable
{
    private readonly IFtpService _ftpService;
    private readonly Func<FtpConfiguration> _configurationFactory;

    private bool _isTransferring;
    private double _transferProgress;

    public TransferViewModel(IFtpService ftpService, Func<FtpConfiguration> configurationFactory)
    {
        _ftpService = ftpService;
        _configurationFactory = configurationFactory;

        UploadCommand = ReactiveCommand.CreateFromTask<TransferRequest>(UploadAsync);
        DownloadCommand = ReactiveCommand.CreateFromTask<TransferRequest>(DownloadAsync);
        DeleteCommand = ReactiveCommand.CreateFromTask<DeleteRequest>(DeleteAsync);
    }

    public bool IsTransferring
    {
        get => _isTransferring;
        set => this.RaiseAndSetIfChanged(ref _isTransferring, value);
    }

    public double TransferProgress
    {
        get => _transferProgress;
        set => this.RaiseAndSetIfChanged(ref _transferProgress, value);
    }

    public ReactiveCommand<TransferRequest, Unit> UploadCommand { get; }
    public ReactiveCommand<TransferRequest, Unit> DownloadCommand { get; }
    public ReactiveCommand<DeleteRequest, Unit> DeleteCommand { get; }

    public event EventHandler<StatusChangedEventArgs>? StatusChanged;

    public async Task<TransferResult> UploadAsync(TransferRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        IsTransferring = true;
        StaticFileLogger.LogInformation($"Starting upload operation");

        var result = new TransferResult();

        try
        {
            var configuration = _configurationFactory();
            var progress = new Progress<double>(p => TransferProgress = p);

            StatusChanged?.Invoke(this, new StatusChangedEventArgs($"Uploading {request.Items.Count} item(s)..."));

            for (int i = 0; i < request.Items.Count; i++)
            {
                var item = request.Items[i];
                try
                {
                    var remotePath = Path.Combine(request.TargetDirectory, item.Name).Replace('\\', '/');

                    if (!item.IsDirectory &&
                        await _ftpService.FileExistsAsync(configuration, remotePath, cancellationToken)
                            .ConfigureAwait(true))
                    {
                        var existingSize = await _ftpService.GetFileSizeAsync(
                            configuration, remotePath, cancellationToken).ConfigureAwait(true);

                        var existingModified = await _ftpService.GetFileModifiedTimeAsync(
                            configuration, remotePath, cancellationToken).ConfigureAwait(true);

                        StatusChanged?.Invoke(this, new StatusChangedEventArgs(
                            $"File {item.Name} already exists (Size: {existingSize} bytes, Modified: {existingModified:g}). Skipping..."));
                        result.SkippedCount++;
                        await Task.Delay(1000, cancellationToken).ConfigureAwait(true);
                        continue;
                    }

                    if (item.IsDirectory)
                    {
                        await UploadDirectoryAsync(configuration, item, request.TargetDirectory, progress,
                                cancellationToken)
                            .ConfigureAwait(true);
                    }
                    else
                    {
                        StatusChanged?.Invoke(this,
                            new StatusChangedEventArgs($"Uploading {item.Name} ({i + 1}/{request.Items.Count})..."));
                        await _ftpService.UploadFileAsync(
                                configuration, item.FullPath, remotePath, progress, cancellationToken)
                            .ConfigureAwait(true);
                    }

                    result.SuccessCount++;
                    ((IProgress<double>)progress).Report((double)(i + 1) / request.Items.Count * 100);
                }
                catch (Exception ex)
                {
                    StaticFileLogger.LogError($"Failed to upload {item.Name}: {ex.Message}");
                    result.FailCount++;
                    result.FailedItems.Add(item.Name);
                }
            }

            StatusChanged?.Invoke(this, new StatusChangedEventArgs(result.SuccessCount > 0
                ? $"Upload complete: {result.SuccessCount} succeeded, {result.FailCount} failed"
                : "Upload failed"));
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke(this, new StatusChangedEventArgs($"Upload failed: {ex.Message}"));
            StaticFileLogger.LogError($"Upload failed: {ex.Message}");
        }
        finally
        {
            IsTransferring = false;
            TransferProgress = 0;
        }

        return result;
    }

    public async Task<TransferResult> DownloadAsync(TransferRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        IsTransferring = true;
        StaticFileLogger.LogInformation($"Starting download operation");

        var result = new TransferResult();

        try
        {
            var configuration = _configurationFactory();
            var progress = new Progress<double>(p => TransferProgress = p);

            StatusChanged?.Invoke(this, new StatusChangedEventArgs($"Downloading {request.Items.Count} item(s)..."));

            for (int i = 0; i < request.Items.Count; i++)
            {
                var item = request.Items[i];
                try
                {
                    var localPath = Path.Combine(request.TargetDirectory, item.Name);

                    if (!item.IsDirectory && File.Exists(localPath))
                    {
                        var localInfo = new FileInfo(localPath);
                        StatusChanged?.Invoke(this, new StatusChangedEventArgs(
                            $"File {item.Name} already exists locally (Size: {localInfo.Length} bytes). Skipping..."));
                        result.SkippedCount++;
                        await Task.Delay(1000, cancellationToken).ConfigureAwait(true);
                        continue;
                    }

                    if (item.IsDirectory)
                    {
                        await DownloadDirectoryAsync(configuration, item, request.TargetDirectory, progress,
                                cancellationToken)
                            .ConfigureAwait(true);
                    }
                    else
                    {
                        StatusChanged?.Invoke(this,
                            new StatusChangedEventArgs($"Downloading {item.Name} ({i + 1}/{request.Items.Count})..."));
                        await _ftpService.DownloadFileAsync(
                                configuration, item.FullPath, localPath, progress, cancellationToken)
                            .ConfigureAwait(true);
                    }

                    result.SuccessCount++;
                    ((IProgress<double>)progress).Report((double)(i + 1) / request.Items.Count * 100);
                }
                catch (Exception ex)
                {
                    StaticFileLogger.LogError($"Failed to download {item.Name}: {ex.Message}");
                    result.FailCount++;
                    result.FailedItems.Add(item.Name);
                }
            }

            StatusChanged?.Invoke(this, new StatusChangedEventArgs(result.SuccessCount > 0
                ? $"Download complete: {result.SuccessCount} succeeded, {result.FailCount} failed"
                : "Download failed"));
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke(this, new StatusChangedEventArgs($"Download failed: {ex.Message}"));
            StaticFileLogger.LogError($"Download failed: {ex.Message}");
        }
        finally
        {
            IsTransferring = false;
            TransferProgress = 0;
        }

        return result;
    }

    public async Task<DeleteResult> DeleteAsync(DeleteRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        IsTransferring = true;

        var result = new DeleteResult();

        try
        {
            var configuration = _configurationFactory();

            StatusChanged?.Invoke(this, new StatusChangedEventArgs($"Deleting {request.Items.Count} item(s)..."));
            StaticFileLogger.LogInformation($"Attempting to delete {request.Items.Count} items");

            foreach (var item in request.Items)
            {
                try
                {
                    StatusChanged?.Invoke(this, new StatusChangedEventArgs($"Deleting {item.Name}..."));

                    if (item.IsDirectory)
                    {
                        await _ftpService.DeleteDirectoryAsync(configuration, item.FullPath, cancellationToken)
                            .ConfigureAwait(true);
                    }
                    else
                    {
                        await _ftpService.DeleteFileAsync(configuration, item.FullPath, cancellationToken)
                            .ConfigureAwait(true);
                    }

                    result.SuccessCount++;
                    StaticFileLogger.LogInformation($"Successfully deleted: {item.Name}");
                }
                catch (WebException ex) when (ex.Response is FtpWebResponse ftpResponse)
                {
                    HandleFtpException(ftpResponse, item.Name, result);
                }
                catch (Exception ex)
                {
                    StaticFileLogger.LogError($"Failed to delete {item.Name}: {ex.Message}");
                    result.FailCount++;
                    result.FailedItems.Add(item.Name);
                }
            }

            StatusChanged?.Invoke(this, new StatusChangedEventArgs(result.SuccessCount > 0
                ? $"Delete complete: {result.SuccessCount} succeeded, {result.FailCount} failed"
                : "Delete failed"));
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke(this, new StatusChangedEventArgs($"Delete operation failed: {ex.Message}"));
            StaticFileLogger.LogError($"Delete operation failed: {ex.Message}");
        }
        finally
        {
            IsTransferring = false;
        }

        return result;
    }

    private async Task UploadDirectoryAsync(
        FtpConfiguration config,
        FileSystemItem directory,
        string remotePath,
        IProgress<double> progress,
        CancellationToken cancellationToken)
    {
        var targetPath = Path.Combine(remotePath, directory.Name).Replace('\\', '/');

        try
        {
            await _ftpService.CreateDirectoryAsync(config, targetPath, cancellationToken).ConfigureAwait(true);
        }
        catch (WebException ex) when (ex.Response is FtpWebResponse ftpResponse)
        {
            if (ftpResponse.StatusCode != FtpStatusCode.ActionNotTakenFileUnavailable)
            {
                StaticFileLogger.LogWarning($"Directory creation issue: {ftpResponse.StatusCode}");
            }
        }

        var files = Directory.GetFiles(directory.FullPath);
        var dirs = Directory.GetDirectories(directory.FullPath);
        var totalItems = files.Length + dirs.Length;
        var currentItem = 0;

        foreach (var file in files)
        {
            try
            {
                var fileName = Path.GetFileName(file);
                var remoteFilePath = Path.Combine(targetPath, fileName).Replace('\\', '/');
                StatusChanged?.Invoke(this, new StatusChangedEventArgs($"Uploading {fileName}..."));

                await _ftpService.UploadFileAsync(config, file, remoteFilePath, progress, cancellationToken)
                    .ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                StaticFileLogger.LogError($"Error uploading {Path.GetFileName(file)}: {ex.Message}");
            }

            currentItem++;
            progress.Report((double)currentItem / totalItems * 100);
        }

        foreach (var dir in dirs)
        {
            var subDir = new FileSystemItem
            {
                Name = Path.GetFileName(dir),
                FullPath = dir,
                IsDirectory = true
            };
            await UploadDirectoryAsync(config, subDir, targetPath, progress, cancellationToken).ConfigureAwait(true);
            currentItem++;
            progress.Report((double)currentItem / totalItems * 100);
        }
    }

    private async Task DownloadDirectoryAsync(
        FtpConfiguration config,
        FileSystemItem directory,
        string localPath,
        IProgress<double> progress,
        CancellationToken cancellationToken)
    {
        var targetPath = Path.Combine(localPath, directory.Name);
        Directory.CreateDirectory(targetPath);

        var items = await _ftpService.ListDirectoryAsync(config, directory.FullPath, cancellationToken)
            .ConfigureAwait(true);

        var totalItems = items.Count();
        var currentItem = 0;

        foreach (var item in items)
        {
            try
            {
                if (item.IsDirectory)
                {
                    await DownloadDirectoryAsync(config, item, targetPath, progress, cancellationToken)
                        .ConfigureAwait(true);
                }
                else
                {
                    var itemPath = Path.Combine(targetPath, item.Name);
                    StatusChanged?.Invoke(this, new StatusChangedEventArgs($"Downloading {item.Name}..."));

                    await _ftpService.DownloadFileAsync(config, item.FullPath, itemPath, progress, cancellationToken)
                        .ConfigureAwait(true);
                }
            }
            catch (Exception ex)
            {
                StaticFileLogger.LogError($"Error downloading {item.Name}: {ex.Message}");
            }

            currentItem++;
            progress.Report((double)currentItem / totalItems * 100);
        }
    }

    private void HandleFtpException(FtpWebResponse ftpResponse, string itemName, DeleteResult result)
    {
        if (ftpResponse.StatusCode == FtpStatusCode.ActionNotTakenFileUnavailable ||
            ftpResponse.StatusCode == FtpStatusCode.ActionNotTakenFilenameNotAllowed)
        {
            var errorMessage = $"Access denied for '{itemName}'";
            StaticFileLogger.LogWarning(errorMessage);
            StatusChanged?.Invoke(this, new StatusChangedEventArgs($"Cannot delete '{itemName}': Access denied"));
        }
        else
        {
            StaticFileLogger.LogError($"FTP error deleting {itemName}: {ftpResponse.StatusDescription}");
        }

        result.FailCount++;
        result.FailedItems.Add(itemName);
    }

    public void Dispose()
    {
        UploadCommand.Dispose();
        DownloadCommand.Dispose();
        DeleteCommand.Dispose();
    }
}
