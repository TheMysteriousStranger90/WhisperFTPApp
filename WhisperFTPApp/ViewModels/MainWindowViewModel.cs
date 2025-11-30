using System.Collections.ObjectModel;
using System.Globalization;
using System.Net;
using System.Reactive;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using ReactiveUI;
using WhisperFTPApp.Commands;
using WhisperFTPApp.Configurations;
using WhisperFTPApp.Logger;
using WhisperFTPApp.Models;
using WhisperFTPApp.Models.Navigations;
using WhisperFTPApp.Services;
using WhisperFTPApp.Services.Interfaces;
using WhisperFTPApp.Views;

namespace WhisperFTPApp.ViewModels;

public sealed class MainWindowViewModel : ReactiveObject, IDisposable
{
    private readonly LocalizationService _localizationService;
    private readonly ISettingsService _settingsService;
    private readonly IFtpService _ftpService;
    private readonly IBackgroundService _backgroundService;

    private string _selectedPath = string.Empty;
    private string _ftpAddress = string.Empty;
    private string _username = string.Empty;
    private string _password = string.Empty;
    private ObservableCollection<string> _localFiles = new();
    private ObservableCollection<string> _ftpFiles = new();
    private ObservableCollection<FileSystemItem> _ftpItems = new();
    public ObservableCollection<string>? DriveNames { get; private set; }
    private double _transferProgress;
    private string _statusMessage = string.Empty;
    private string _currentDirectory = "/";
    private FileSystemItem? _selectedFtpItem;
    private string _selectedLocalFile = string.Empty;
    private ObservableCollection<DriveInfo> _availableDrives = new();
    private DriveInfo? _selectedDrive;
    private ObservableCollection<FileSystemItem> _localItems = new();

    private FileSystemItem? _selectedLocalItem;
    private string _localCurrentPath = string.Empty;
    private FileStats _localFileStats = new();
    private FileStats _remoteFileStats = new();
    private string _port = "21";
    private bool _isConnected;
    private bool _isTransferring;
    private readonly ObservableCollection<BreadcrumbItem> _breadcrumbs = new();
    private readonly ObservableCollection<FtpConnectionEntity> _recentConnections = new();
    private FtpConnectionEntity? _selectedRecentConnection;
    private Control _currentView;
    private readonly Control _mainView;
    private readonly Control _settingsView;
    private readonly Control _scanView;
    private string _backgroundPath = string.Empty;
    private int _timeout = 10000;
    private int _readWriteTimeout = 30000;
    private bool _enableSsl;
    private bool _usePassive = true;
    private int _bufferSize = 131072;
    private int _maxRetries = 3;
    private int _retryDelay = 2000;

    public int Timeout
    {
        get => _timeout;
        set => this.RaiseAndSetIfChanged(ref _timeout, value);
    }

    public int ReadWriteTimeout
    {
        get => _readWriteTimeout;
        set => this.RaiseAndSetIfChanged(ref _readWriteTimeout, value);
    }

    public bool EnableSsl
    {
        get => _enableSsl;
        set => this.RaiseAndSetIfChanged(ref _enableSsl, value);
    }

    public bool UsePassive
    {
        get => _usePassive;
        set => this.RaiseAndSetIfChanged(ref _usePassive, value);
    }

    public int BufferSize
    {
        get => _bufferSize;
        set => this.RaiseAndSetIfChanged(ref _bufferSize, value);
    }

    public int MaxRetries
    {
        get => _maxRetries;
        set => this.RaiseAndSetIfChanged(ref _maxRetries, value);
    }

    public int RetryDelay
    {
        get => _retryDelay;
        set => this.RaiseAndSetIfChanged(ref _retryDelay, value);
    }

    private readonly ObservableCollection<FileSystemItem> _selectedLocalItems = new();
    private readonly ObservableCollection<FileSystemItem> _selectedFtpItems = new();

    public ObservableCollection<FileSystemItem> SelectedLocalItems => _selectedLocalItems;
    public ObservableCollection<FileSystemItem> SelectedFtpItems => _selectedFtpItems;

    public string Port
    {
        get => _port;
        set => this.RaiseAndSetIfChanged(ref _port, value);
    }

    public bool IsConnected
    {
        get => _isConnected;
        set => this.RaiseAndSetIfChanged(ref _isConnected, value);
    }

    public bool IsTransferring
    {
        get => _isTransferring;
        set => this.RaiseAndSetIfChanged(ref _isTransferring, value);
    }

    public DriveInfo? SelectedDrive
    {
        get => _selectedDrive;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedDrive, value);
            if (value != null && value.IsReady)
            {
                try
                {
                    LocalCurrentPath = value.RootDirectory.FullName;
                    _ = RefreshLocalFiles();
                    StatusMessage = $"Selected drive: {value.Name}";
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Error accessing drive: {ex.Message}";
                }
            }
        }
    }

    public string LocalCurrentPath
    {
        get => _localCurrentPath;
        set => this.RaiseAndSetIfChanged(ref _localCurrentPath, value);
    }

    public FileSystemItem? SelectedLocalItem
    {
        get => _selectedLocalItem;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedLocalItem, value);
            UpdateLocalStats();
            if (value?.IsDirectory == true)
            {
                NavigateToLocalDirectory(value.FullPath);
            }
        }
    }

    public FileStats LocalFileStats
    {
        get => _localFileStats;
        set => this.RaiseAndSetIfChanged(ref _localFileStats, value);
    }

    public FileStats RemoteFileStats
    {
        get => _remoteFileStats;
        set => this.RaiseAndSetIfChanged(ref _remoteFileStats, value);
    }

    public ObservableCollection<BreadcrumbItem> Breadcrumbs => _breadcrumbs;
    public ReactiveCommand<NavigationItem, Unit> NavigateCommand { get; }
    public ICommand NavigateToPathCommand { get; }

    public string StatusMessage
    {
        get => _statusMessage;
        set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
    }

    public string CurrentDirectory
    {
        get => _currentDirectory;
        set => this.RaiseAndSetIfChanged(ref _currentDirectory, value);
    }

    public FileSystemItem? SelectedFtpItem
    {
        get => _selectedFtpItem;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedFtpItem, value);
            UpdateRemoteStats();
        }
    }

    public string SelectedLocalFile
    {
        get => _selectedLocalFile;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedLocalFile, value);
            UpdateLocalStats();
        }
    }

    public double TransferProgress
    {
        get => _transferProgress;
        set => this.RaiseAndSetIfChanged(ref _transferProgress, value);
    }

    public string SelectedPath
    {
        get => _selectedPath;
        set => this.RaiseAndSetIfChanged(ref _selectedPath, value);
    }

    public string FtpAddress
    {
        get => _ftpAddress;
        set => this.RaiseAndSetIfChanged(ref _ftpAddress, value);
    }

    public string Username
    {
        get => _username;
        set => this.RaiseAndSetIfChanged(ref _username, value);
    }

    public string Password
    {
        get => _password;
        set => this.RaiseAndSetIfChanged(ref _password, value);
    }

    public ObservableCollection<FileSystemItem> LocalItems
    {
        get => _localItems;
        private set => this.RaiseAndSetIfChanged(ref _localItems, value);
    }

    public ObservableCollection<string> LocalFiles
    {
        get => _localFiles;
        private set => this.RaiseAndSetIfChanged(ref _localFiles, value);
    }

    public ObservableCollection<string> FtpFiles
    {
        get => _ftpFiles;
        private set => this.RaiseAndSetIfChanged(ref _ftpFiles, value);
    }

    public ObservableCollection<FileSystemItem> FtpItems
    {
        get => _ftpItems;
        private set => this.RaiseAndSetIfChanged(ref _ftpItems, value);
    }

    public ObservableCollection<DriveInfo> AvailableDrives
    {
        get => _availableDrives;
        private set => this.RaiseAndSetIfChanged(ref _availableDrives, value);
    }

    public ReactiveCommand<FileSystemItem, Unit> NavigateToLocalDirectoryCommand { get; }
    public ReactiveCommand<Unit, Unit> NavigateLocalUpCommand { get; }
    public ReactiveCommand<Unit, Unit> RefreshLocalCommand { get; }
    public ReactiveCommand<Unit, Unit> ConnectCommand { get; }
    public ReactiveCommand<Unit, Unit> UploadCommand { get; }
    public ReactiveCommand<Unit, Unit> BrowseCommand { get; }
    public ReactiveCommand<Unit, Unit> DownloadCommand { get; }
    public ReactiveCommand<Unit, Unit> DeleteCommand { get; }
    public ReactiveCommand<Unit, Unit> RefreshCommand { get; }
    public ReactiveCommand<Unit, Unit> NavigateUpCommand { get; }
    public ReactiveCommand<FileSystemItem, Unit> NavigateToFolderCommand { get; }
    public ReactiveCommand<Unit, Unit> SaveConnectionCommand { get; }
    public ReactiveCommand<Unit, Unit> DisconnectCommand { get; }
    public ReactiveCommand<Unit, Unit> CleanCommand { get; }
    public ObservableCollection<FtpConnectionEntity> RecentConnections => _recentConnections;
    public ReactiveCommand<Unit, Unit> ShowRecentConnectionsCommand { get; }
    public ReactiveCommand<FtpConnectionEntity, Unit> DeleteConnectionCommand { get; }
    public ReactiveCommand<Unit, Unit> ShowMainViewCommand { get; }
    public ReactiveCommand<Unit, Unit> ShowScanViewCommand { get; }
    public ReactiveCommand<Unit, Unit> ShowSettingsCommand { get; }

    public FtpConnectionEntity? SelectedRecentConnection
    {
        get => _selectedRecentConnection;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedRecentConnection, value);
            if (value != null)
            {
                _ = SwitchConnectionAsync(value);
            }
        }
    }

    public Control CurrentView
    {
        get => _currentView;
        private set => this.RaiseAndSetIfChanged(ref _currentView, value);
    }

    public string BackgroundPath
    {
        get => _backgroundPath;
        private set => this.RaiseAndSetIfChanged(ref _backgroundPath, value);
    }

    public MainWindowViewModel(
        IFtpService ftpService,
        ISettingsService settingsService,
        IBackgroundService backgroundService,
        IWifiScannerService scannerService)
    {
        ArgumentNullException.ThrowIfNull(ftpService);
        ArgumentNullException.ThrowIfNull(settingsService);
        ArgumentNullException.ThrowIfNull(backgroundService);
        ArgumentNullException.ThrowIfNull(scannerService);

        _localizationService = LocalizationService.Instance;
        _settingsService = settingsService;
        _ftpService = ftpService;
        _backgroundService = backgroundService;

        NavigateCommand = ReactiveCommand.CreateFromTask<NavigationItem>(NavigateToItemAsync);
        NavigateUpCommand = ReactiveCommand.CreateFromTask(NavigateUpAsync);
        NavigateToFolderCommand = ReactiveCommand.CreateFromTask<FileSystemItem>(NavigateToFolderAsync);
        SaveConnectionCommand = ReactiveCommand.CreateFromTask(SaveSuccessfulConnection);
        UploadCommand = ReactiveCommand.CreateFromTask(UploadFileAsync);
        BrowseCommand = ReactiveCommand.CreateFromTask(BrowseFilesAsync);
        DownloadCommand = ReactiveCommand.CreateFromTask(DownloadFileAsync);
        DeleteCommand = ReactiveCommand.CreateFromTask(DeleteFileAsync);
        RefreshCommand = ReactiveCommand.CreateFromTask(RefreshDirectoryAsync);
        NavigateLocalUpCommand = ReactiveCommand.Create(NavigateLocalUp);
        RefreshLocalCommand =
            ReactiveCommand.CreateFromTask(async () => await RefreshLocalFiles().ConfigureAwait(false));
        NavigateToLocalDirectoryCommand = ReactiveCommand.Create<FileSystemItem>(NavigateToLocalDirectory);
        ConnectCommand = ReactiveCommand.CreateFromTask(
            ConnectToFtpAsync,
            this.WhenAnyValue(x => x.IsConnected, connected => !connected));

        DisconnectCommand = ReactiveCommand.CreateFromTask(
            DisconnectAsync,
            this.WhenAnyValue(x => x.IsConnected));
        CleanCommand = ReactiveCommand.Create(CleanFields);

        NavigateToPathCommand = new BreadcrumbNavigationCommand(path =>
        {
            if (!string.IsNullOrEmpty(path))
            {
                CurrentDirectory = path;
                UpdateBreadcrumbs();
                _ = RefreshDirectoryAsync();
            }
        });
        ShowRecentConnectionsCommand = ReactiveCommand.Create(() => { });
        DeleteConnectionCommand = ReactiveCommand.CreateFromTask<FtpConnectionEntity>(DeleteConnectionAsync);

        var mainView = new MainView { DataContext = this };
        var settingsView = new SettingsView
        {
            DataContext = new SettingsWindowViewModel(
                settingsService,
                backgroundService,
                _localizationService)
        };
        var scanView = new ScanView { DataContext = new ScanWindowViewModel(scannerService) };

        _mainView = mainView;
        _settingsView = settingsView;
        _scanView = scanView;
        _currentView = mainView;

        ShowMainViewCommand = ReactiveCommand.Create(() =>
        {
            CurrentView = _mainView;
            return Unit.Default;
        });
        ShowSettingsCommand = ReactiveCommand.Create(() =>
        {
            CurrentView = _settingsView;
            return Unit.Default;
        });
        ShowScanViewCommand = ReactiveCommand.Create(() =>
        {
            CurrentView = _scanView;
            return Unit.Default;
        });

        BackgroundPath = backgroundService.CurrentBackground;
        _backgroundService.BackgroundChanged.Subscribe(path => BackgroundPath = path);

        _ = LoadRecentConnectionsAsync();
        InitializeLocalNavigation();
    }

    private void NavigateToLocalDirectory(FileSystemItem? item)
    {
        if (item == null || !item.IsDirectory) return;

        LocalCurrentPath = item.FullPath;
        _ = RefreshLocalFiles();
    }

    private void NavigateToLocalDirectory(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        LocalCurrentPath = path;
        _ = RefreshLocalFiles();
    }

    private void UpdateRemoteStats()
    {
        RemoteFileStats = new FileStats
        {
            TotalItems = FtpItems?.Count ?? 0,
            TotalSize = FtpItems?.Sum(f => f.Size) ?? 0,
            SelectedItems = SelectedFtpItem != null ? 1 : 0,
            SelectedSize = SelectedFtpItem?.Size ?? 0
        };
    }

    private async Task BrowseFilesAsync()
    {
        var topLevel = TopLevel.GetTopLevel(_currentView);
        if (topLevel?.StorageProvider == null) return;

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select Folder",
            AllowMultiple = false
        }).ConfigureAwait(true);

        if (folders.Count > 0)
        {
            var selectedPath = folders[0].Path.LocalPath;
            SelectedPath = selectedPath;
            var files = Directory.GetFiles(selectedPath);
            _localFiles.Clear();
            foreach (var file in files)
            {
                _localFiles.Add(file);
            }
        }
    }

    private async Task ConnectToFtpAsync()
    {
        IsTransferring = true;
        StaticFileLogger.LogInformation($"Attempting to connect to {FtpAddress}");
        try
        {
            StatusMessage = "Connecting to FTP server...";
            _ftpItems.Clear();
            UpdateRemoteStats();

            var configuration = CreateConfiguration();

            bool isConnected = await _ftpService.ConnectAsync(configuration).ConfigureAwait(true);
            if (isConnected)
            {
                IsConnected = true;
                StatusMessage = "Connected successfully";
                StaticFileLogger.LogInformation($"Successfully connected to {FtpAddress}");

                var items = await _ftpService.ListDirectoryAsync(
                    configuration,
                    CurrentDirectory,
                    CancellationToken.None).ConfigureAwait(true);

                FtpItems = new ObservableCollection<FileSystemItem>(items);
                UpdateRemoteStats();

                await SaveSuccessfulConnection().ConfigureAwait(true);
            }
            else
            {
                IsConnected = false;
                StatusMessage = "Failed to connect. Please check your credentials.";
                StaticFileLogger.LogError($"Failed to connect to {FtpAddress}");
            }
        }
        catch (Exception ex)
        {
            IsConnected = false;
            StatusMessage = $"Connection error: {ex.Message}";
            StaticFileLogger.LogError($"Connection error: {ex.Message}");
            _ftpItems.Clear();
            UpdateRemoteStats();
        }
        finally
        {
            IsTransferring = false;
        }
    }

    private async Task UploadFileAsync()
    {
        IsTransferring = true;
        StaticFileLogger.LogInformation($"Starting upload operation from {LocalCurrentPath}");

        List<FileSystemItem> itemsToUpload;

        if (SelectedLocalItems.Count > 0)
        {
            itemsToUpload = SelectedLocalItems.Where(i => !i.Name.Equals("..", StringComparison.Ordinal)).ToList();
        }
        else if (SelectedLocalItem != null)
        {
            itemsToUpload = new List<FileSystemItem> { SelectedLocalItem };
        }
        else
        {
            itemsToUpload = new List<FileSystemItem>();
        }

        if (itemsToUpload.Count == 0)
        {
            StatusMessage = "No items selected for upload";
            IsTransferring = false;
            return;
        }

        try
        {
            var configuration = CreateConfiguration();
            var progress = new Progress<double>(p => TransferProgress = p);
            int successCount = 0;
            int failCount = 0;

            StatusMessage = $"Uploading {itemsToUpload.Count} item(s)...";

            for (int i = 0; i < itemsToUpload.Count; i++)
            {
                var item = itemsToUpload[i];
                try
                {
                    string targetDirectory = SelectedFtpItem?.IsDirectory == true
                        ? SelectedFtpItem.FullPath
                        : CurrentDirectory;

                    var remotePath = Path.Combine(targetDirectory, item.Name).Replace('\\', '/');

                    if (!item.IsDirectory &&
                        await _ftpService.FileExistsAsync(configuration, remotePath, CancellationToken.None)
                            .ConfigureAwait(true))
                    {
                        var existingSize = await _ftpService.GetFileSizeAsync(
                            configuration,
                            remotePath,
                            CancellationToken.None).ConfigureAwait(true);

                        var existingModified = await _ftpService.GetFileModifiedTimeAsync(
                            configuration,
                            remotePath,
                            CancellationToken.None).ConfigureAwait(true);

                        StatusMessage =
                            $"File {item.Name} already exists (Size: {existingSize} bytes, Modified: {existingModified:g}). Skipping...";
                        StaticFileLogger.LogInformation($"Skipping existing file: {item.Name}");
                        await Task.Delay(1000).ConfigureAwait(true);
                        continue;
                    }

                    if (item.IsDirectory)
                    {
                        await UploadDirectoryAsync(configuration, item, targetDirectory, progress).ConfigureAwait(true);
                    }
                    else
                    {
                        StatusMessage = $"Uploading {item.Name} ({i + 1}/{itemsToUpload.Count})...";
                        await _ftpService.UploadFileAsync(
                            configuration,
                            item.FullPath,
                            remotePath,
                            progress,
                            CancellationToken.None).ConfigureAwait(true);
                    }

                    successCount++;
                    ((IProgress<double>)progress).Report((double)(i + 1) / itemsToUpload.Count * 100);
                }
                catch (Exception ex)
                {
                    StaticFileLogger.LogError($"Failed to upload {item.Name}: {ex.Message}");
                    failCount++;
                }
            }

            await RefreshDirectoryAsync().ConfigureAwait(true);

            if (itemsToUpload.Count > 1)
            {
                StatusMessage = $"Upload complete: {successCount} succeeded, {failCount} failed";
            }
            else
            {
                StatusMessage = successCount > 0 ? "Upload complete" : "Upload failed";
            }

            StaticFileLogger.LogInformation($"Upload completed: {successCount} succeeded, {failCount} failed");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Upload failed: {ex.Message}";
            StaticFileLogger.LogError($"Upload failed: {ex.Message}");
        }
        finally
        {
            IsTransferring = false;
            TransferProgress = 0;
        }
    }

#pragma warning disable SYSLIB0014 // WebRequest is obsolete but required for FTP
    private async Task UploadDirectoryAsync(
        FtpConfiguration config,
        FileSystemItem directory,
        string remotePath,
        IProgress<double> progress)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentNullException.ThrowIfNull(remotePath);

        var targetPath = Path.Combine(remotePath, directory.Name).Replace('\\', '/');

        try
        {
            await _ftpService.CreateDirectoryAsync(
                config,
                targetPath,
                CancellationToken.None).ConfigureAwait(true);
        }
        catch (WebException)
        {
            // Directory might already exist, continue
        }

        var files = Directory.GetFiles(directory.FullPath);
        var dirs = Directory.GetDirectories(directory.FullPath);
        var totalItems = files.Length + dirs.Length;
        var currentItem = 0;

        foreach (var file in files)
        {
            var fileName = Path.GetFileName(file);
            var remoteFilePath = Path.Combine(targetPath, fileName).Replace('\\', '/');
            StatusMessage = $"Uploading {fileName}...";

            await _ftpService.UploadFileAsync(
                config,
                file,
                remoteFilePath,
                progress,
                CancellationToken.None).ConfigureAwait(true);

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
            await UploadDirectoryAsync(config, subDir, targetPath, progress).ConfigureAwait(true);
            currentItem++;
            progress.Report((double)currentItem / totalItems * 100);
        }
    }
#pragma warning restore SYSLIB0014

    private async Task DownloadFileAsync()
    {
        IsTransferring = true;
        StaticFileLogger.LogInformation($"Starting download operation");

        List<FileSystemItem> itemsToDownload;

        if (SelectedFtpItems.Count > 0)
        {
            itemsToDownload = SelectedFtpItems.ToList();
        }
        else if (SelectedFtpItem != null)
        {
            itemsToDownload = new List<FileSystemItem> { SelectedFtpItem };
        }
        else
        {
            itemsToDownload = new List<FileSystemItem>();
        }

        if (itemsToDownload.Count == 0)
        {
            StatusMessage = "No items selected for download";
            IsTransferring = false;
            return;
        }

        try
        {
            var configuration = CreateConfiguration();
            var progress = new Progress<double>(p => TransferProgress = p);
            int successCount = 0;
            int failCount = 0;

            StatusMessage = $"Downloading {itemsToDownload.Count} item(s)...";

            for (int i = 0; i < itemsToDownload.Count; i++)
            {
                var item = itemsToDownload[i];
                try
                {
                    var localPath = Path.Combine(LocalCurrentPath, item.Name);

                    if (!item.IsDirectory && File.Exists(localPath))
                    {
                        var localInfo = new FileInfo(localPath);
                        StatusMessage =
                            $"File {item.Name} already exists locally (Size: {localInfo.Length} bytes, Modified: {localInfo.LastWriteTime:g}). Skipping...";
                        StaticFileLogger.LogInformation($"Skipping existing local file: {item.Name}");
                        await Task.Delay(1000).ConfigureAwait(true);
                        continue;
                    }

                    if (item.IsDirectory)
                    {
                        await DownloadDirectoryAsync(configuration, item, LocalCurrentPath, progress)
                            .ConfigureAwait(true);
                    }
                    else
                    {
                        StatusMessage = $"Downloading {item.Name} ({i + 1}/{itemsToDownload.Count})...";
                        await _ftpService.DownloadFileAsync(
                            configuration,
                            item.FullPath,
                            localPath,
                            progress,
                            CancellationToken.None).ConfigureAwait(true);
                    }

                    successCount++;
                    ((IProgress<double>)progress).Report((double)(i + 1) / itemsToDownload.Count * 100);
                }
                catch (Exception ex)
                {
                    StaticFileLogger.LogError($"Failed to download {item.Name}: {ex.Message}");
                    failCount++;
                }
            }

            await RefreshLocalFiles().ConfigureAwait(true);

            if (itemsToDownload.Count > 1)
            {
                StatusMessage = $"Download complete: {successCount} succeeded, {failCount} failed";
            }
            else
            {
                StatusMessage = successCount > 0 ? "Download complete" : "Download failed";
            }

            StaticFileLogger.LogInformation($"Download completed: {successCount} succeeded, {failCount} failed");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Download failed: {ex.Message}";
            StaticFileLogger.LogError($"Download failed: {ex.Message}");
        }
        finally
        {
            IsTransferring = false;
            TransferProgress = 0;
        }
    }

    private async Task DownloadDirectoryAsync(
        FtpConfiguration config,
        FileSystemItem directory,
        string localPath,
        IProgress<double> progress)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentNullException.ThrowIfNull(localPath);

        var targetPath = Path.Combine(localPath, directory.Name);
        Directory.CreateDirectory(targetPath);

        var items = await _ftpService.ListDirectoryAsync(
            config,
            directory.FullPath,
            CancellationToken.None).ConfigureAwait(true);

        var totalItems = items.Count();
        var currentItem = 0;

        foreach (var item in items)
        {
            if (item.IsDirectory)
            {
                await DownloadDirectoryAsync(config, item, targetPath, progress).ConfigureAwait(true);
            }
            else
            {
                var itemPath = Path.Combine(targetPath, item.Name);
                StatusMessage = $"Downloading {item.Name}...";
                await _ftpService.DownloadFileAsync(
                    config,
                    item.FullPath,
                    itemPath,
                    progress,
                    CancellationToken.None).ConfigureAwait(true);
            }

            currentItem++;
            progress.Report((double)currentItem / totalItems * 100);
        }
    }

    private async Task DeleteFileAsync()
    {
        List<FileSystemItem> itemsToDelete;

        if (SelectedFtpItems.Count > 0)
        {
            itemsToDelete = SelectedFtpItems.ToList();
        }
        else if (SelectedFtpItem != null)
        {
            itemsToDelete = new List<FileSystemItem> { SelectedFtpItem };
        }
        else
        {
            itemsToDelete = new List<FileSystemItem>();
        }

        if (itemsToDelete.Count == 0)
        {
            StatusMessage = "No items selected for deletion";
            return;
        }

        IsTransferring = true;

        try
        {
            var configuration = CreateConfiguration();
            int successCount = 0;
            int failCount = 0;

            StatusMessage = $"Deleting {itemsToDelete.Count} item(s)...";
            StaticFileLogger.LogInformation($"Attempting to delete {itemsToDelete.Count} items");

            foreach (var item in itemsToDelete)
            {
                try
                {
                    StatusMessage = $"Deleting {item.Name}...";

                    if (item.IsDirectory)
                    {
                        await _ftpService.DeleteDirectoryAsync(
                            configuration,
                            item.FullPath,
                            CancellationToken.None).ConfigureAwait(true);
                    }
                    else
                    {
                        await _ftpService.DeleteFileAsync(
                            configuration,
                            item.FullPath,
                            CancellationToken.None).ConfigureAwait(true);
                    }

                    successCount++;
                    StaticFileLogger.LogInformation($"Successfully deleted: {item.Name}");
                }
                catch (Exception ex)
                {
                    StaticFileLogger.LogError($"Failed to delete {item.Name}: {ex.Message}");
                    failCount++;
                }
            }

            SelectedFtpItem = null;
            SelectedFtpItems.Clear();
            await RefreshDirectoryAsync().ConfigureAwait(true);

            if (itemsToDelete.Count > 1)
            {
                StatusMessage = $"Delete complete: {successCount} succeeded, {failCount} failed";
            }
            else
            {
                StatusMessage = successCount > 0 ? $"Successfully deleted {itemsToDelete[0].Name}" : "Delete failed";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Delete failed: {ex.Message}";
            StaticFileLogger.LogError($"Delete operation failed: {ex.Message}");
        }
        finally
        {
            IsTransferring = false;
        }
    }

    private async Task RefreshDirectoryAsync()
    {
        try
        {
            StatusMessage = "Refreshing directory...";
            var configuration = CreateConfiguration();

            var items = await _ftpService.ListDirectoryAsync(
                configuration,
                CurrentDirectory,
                CancellationToken.None).ConfigureAwait(true);

            _ftpItems.Clear();
            foreach (var item in items)
            {
                _ftpItems.Add(item);
            }

            StatusMessage = "Directory refreshed";
            UpdateRemoteStats();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Refresh failed: {ex.Message}";
            _ftpItems.Clear();
            UpdateRemoteStats();
        }
    }

    private Task RefreshLocalFiles()
    {
        try
        {
            var items = new List<FileSystemItem>();
            var currentDir = new DirectoryInfo(LocalCurrentPath);

            if (currentDir.Parent != null && !string.IsNullOrEmpty(currentDir.Parent.FullName))
            {
                items.Add(new FileSystemItem
                {
                    Name = "..",
                    FullPath = currentDir.Parent.FullName,
                    IsDirectory = true,
                    Modified = currentDir.Parent.LastWriteTime,
                    Type = "Directory"
                });
            }

            foreach (var dir in currentDir.GetDirectories())
            {
                try
                {
                    items.Add(new FileSystemItem
                    {
                        Name = dir.Name,
                        FullPath = dir.FullName,
                        IsDirectory = true,
                        Modified = dir.LastWriteTime,
                        Type = "Directory"
                    });
                }
                catch (UnauthorizedAccessException ex)
                {
                    StaticFileLogger.LogError($"Access denied: {ex.Message}");
                }
            }

            foreach (var file in currentDir.GetFiles())
            {
                try
                {
                    items.Add(new FileSystemItem
                    {
                        Name = file.Name,
                        FullPath = file.FullName,
                        IsDirectory = false,
                        Size = file.Length,
                        Modified = file.LastWriteTime,
                        Type = file.Extension
                    });
                }
                catch (UnauthorizedAccessException ex)
                {
                    StaticFileLogger.LogError($"Access denied: {ex.Message}");
                }
            }

            LocalItems = new ObservableCollection<FileSystemItem>(items);
            UpdateLocalStats();
            StatusMessage = $"Loaded {items.Count} items from {LocalCurrentPath}";
        }
        catch (UnauthorizedAccessException ex)
        {
            StatusMessage = $"Access denied: {ex.Message}";
            StaticFileLogger.LogError($"Access denied: {ex.Message}");
            LocalItems = new ObservableCollection<FileSystemItem>();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error accessing directory: {ex.Message}";
            StaticFileLogger.LogError($"Error accessing directory: {ex.Message}");
            LocalItems = new ObservableCollection<FileSystemItem>();
        }

        return Task.CompletedTask;
    }

    private void UpdateLocalStats()
    {
        LocalFileStats = new FileStats
        {
            TotalItems = LocalItems.Count(x => !x.IsDirectory),
            TotalSize = LocalItems.Where(x => !x.IsDirectory).Sum(f => f.Size),
            SelectedItems = SelectedLocalItem != null ? 1 : 0,
            SelectedSize = SelectedLocalItem?.Size ?? 0
        };
    }

    private async Task NavigateUpAsync()
    {
        if (CurrentDirectory == "/") return;
        CurrentDirectory = Path.GetDirectoryName(CurrentDirectory)?.Replace('\\', '/') ?? "/";
        UpdateBreadcrumbs();
        await RefreshDirectoryAsync().ConfigureAwait(true);
    }

    private async Task NavigateToFolderAsync(FileSystemItem? item)
    {
        try
        {
            if (item == null)
            {
                StaticFileLogger.LogError("Navigation attempted with null item");
                return;
            }

            if (!IsConnected)
            {
                StaticFileLogger.LogError("Cannot navigate: Not connected to FTP server");
                StatusMessage = "Please connect to FTP server first";
                return;
            }

            if (!item.IsDirectory)
            {
                StaticFileLogger.LogInformation($"Skipping navigation for non-directory item: {item.Name}");
                return;
            }

            StaticFileLogger.LogInformation($"Navigating to: {item.FullPath}");
            CurrentDirectory = item.FullPath;
            UpdateBreadcrumbs();
            await RefreshDirectoryAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            StaticFileLogger.LogError($"Navigation failed: {ex.Message}");
            StatusMessage = $"Navigation failed: {ex.Message}";
        }
    }

    private async Task NavigateToItemAsync(NavigationItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        CurrentDirectory = item.Path;
        UpdateBreadcrumbs();
        await RefreshDirectoryAsync().ConfigureAwait(true);
    }

    private void UpdateBreadcrumbs()
    {
        _breadcrumbs.Clear();
        var parts = CurrentDirectory.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var path = "/";
        _breadcrumbs.Add(new BreadcrumbItem(path, "Root", NavigateToPath));

        foreach (var part in parts)
        {
            path = Path.Combine(path, part).Replace('\\', '/');
            _breadcrumbs.Add(new BreadcrumbItem(path, part, NavigateToPath));
        }
    }

    private void NavigateToPath(string path)
    {
        CurrentDirectory = path;
        UpdateBreadcrumbs();
        _ = RefreshDirectoryAsync();
    }

    private async Task SaveSuccessfulConnection()
    {
        try
        {
            Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"[{DateTime.Now:HH:mm:ss}] Saving successful connection: {FtpAddress}"));
            var connection = new FtpConnectionEntity
            {
                Name = FtpAddress,
                Address = FtpAddress,
                Username = Username,
                Password = Password,
                LastUsed = DateTime.UtcNow
            };

            var connections = _recentConnections.ToList();
            var existing = connections.FirstOrDefault(c =>
                c.Address.Equals(connection.Address, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
                    $"[{DateTime.Now:HH:mm:ss}] Updating existing connection"));
                connections.Remove(existing);
            }

            connections.Add(connection);
            var toSave = connections.OrderByDescending(c => c.LastUsed).Take(10).ToList();

            await _settingsService.SaveConnectionsAsync(toSave).ConfigureAwait(true);
            Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"[{DateTime.Now:HH:mm:ss}] Connection saved successfully"));
            await LoadRecentConnectionsAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"[{DateTime.Now:HH:mm:ss}] Error saving connection: {ex.Message}"));
        }
    }

    private async Task LoadRecentConnectionsAsync()
    {
        try
        {
            var connections = await _settingsService.LoadConnectionsAsync().ConfigureAwait(true) ??
                              new List<FtpConnectionEntity>();
            _recentConnections.Clear();
            foreach (var connection in connections.OrderByDescending(c => c.LastUsed))
            {
                _recentConnections.Add(connection);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading connections: {ex.Message}";
            _recentConnections.Clear();
        }
    }

    private FtpConfiguration CreateConfiguration()
    {
        if (!int.TryParse(Port, out int portNumber))
        {
            throw new InvalidOperationException("Invalid port number");
        }

        var uri = new Uri(FtpAddress.StartsWith("ftp://", StringComparison.OrdinalIgnoreCase)
            ? FtpAddress
            : $"ftp://{FtpAddress}");
        var baseAddress = $"{uri.Scheme}://{uri.Host}";

        return new FtpConfiguration
        {
            FtpAddress = baseAddress,
            Username = Username,
            Password = Password,
            Port = portNumber,
            Timeout = Timeout,
            ReadWriteTimeout = ReadWriteTimeout,
            EnableSsl = EnableSsl,
            UsePassive = UsePassive,
            UseBinary = true,
            KeepAlive = true,
            BufferSize = BufferSize,
            MaxRetries = MaxRetries,
            RetryDelay = RetryDelay
        };
    }

    private void InitializeLocalNavigation()
    {
        AvailableDrives = new ObservableCollection<DriveInfo>(DriveInfo.GetDrives());
        LocalItems = new ObservableCollection<FileSystemItem>();
        SelectedDrive = AvailableDrives.FirstOrDefault();
    }

    private void NavigateLocalUp()
    {
        var parent = Directory.GetParent(LocalCurrentPath);
        if (parent != null)
        {
            NavigateToLocalDirectory(parent.FullName);
        }
    }

    private async Task DisconnectAsync()
    {
        StaticFileLogger.LogInformation("Disconnecting from FTP server");
        try
        {
            await _ftpService.DisconnectAsync(CancellationToken.None).ConfigureAwait(true);
            _ftpItems.Clear();
            UpdateRemoteStats();
            IsConnected = false;
            StatusMessage = "Disconnected from FTP server";
            StaticFileLogger.LogInformation("Successfully disconnected from FTP server");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error disconnecting: {ex.Message}";
            StaticFileLogger.LogError($"Disconnect failed: {ex.Message}");
        }
    }

    private void CleanFields()
    {
        FtpAddress = string.Empty;
        Username = string.Empty;
        Password = string.Empty;
        Port = "21";
        StatusMessage = "Fields cleared";
    }

    private async Task SwitchConnectionAsync(FtpConnectionEntity connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        try
        {
            if (IsConnected)
            {
                await DisconnectAsync().ConfigureAwait(true);
            }

            FtpAddress = connection.Address;
            Username = connection.Username;
            Password = connection.Password;
            await ConnectToFtpAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error switching connection: {ex.Message}";
        }
        finally
        {
            SelectedRecentConnection = null;
        }
    }

    private async Task DeleteConnectionAsync(FtpConnectionEntity connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        try
        {
            await _settingsService.DeleteConnectionAsync(connection).ConfigureAwait(true);
            _recentConnections.Remove(connection);
            StatusMessage = "Connection removed from history";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error removing connection: {ex.Message}";
        }
    }

    public void Dispose()
    {
        NavigateCommand?.Dispose();
        NavigateUpCommand?.Dispose();
        NavigateToFolderCommand?.Dispose();
        SaveConnectionCommand?.Dispose();
        UploadCommand?.Dispose();
        BrowseCommand?.Dispose();
        DownloadCommand?.Dispose();
        DeleteCommand?.Dispose();
        RefreshCommand?.Dispose();
        NavigateLocalUpCommand?.Dispose();
        RefreshLocalCommand?.Dispose();
        NavigateToLocalDirectoryCommand?.Dispose();
        ConnectCommand?.Dispose();
        DisconnectCommand?.Dispose();
        CleanCommand?.Dispose();
        ShowRecentConnectionsCommand?.Dispose();
        DeleteConnectionCommand?.Dispose();
        ShowMainViewCommand?.Dispose();
        ShowScanViewCommand?.Dispose();
        ShowSettingsCommand?.Dispose();
    }
}
