using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Reactive;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using ReactiveUI;
using WhisperFTPApp.Commands;
using WhisperFTPApp.Configurations;
using WhisperFTPApp.Models;
using WhisperFTPApp.Models.Navigations;
using WhisperFTPApp.Services.Interfaces;
using WhisperFTPApp.Views;

namespace WhisperFTPApp.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private readonly ISettingsService _settingsService;
    private string _selectedPath;
    private string _ftpAddress;
    private string _username;
    private string _password;
    private ObservableCollection<string> _localFiles;
    private ObservableCollection<string> _ftpFiles;
    private ObservableCollection<FileSystemItem> _ftpItems;
    private double _transferProgress;
    private readonly IFtpService _ftpService;
    private string _statusMessage;
    private string _currentDirectory = "/";
    private FileSystemItem _selectedFtpItem;
    private string _selectedLocalFile;
    private ObservableCollection<DriveInfo> _availableDrives;
    private DriveInfo _selectedDrive;
    private FileSystemItem _rootDirectory;
    private ObservableCollection<FileSystemItem> _localItems;
    private FileSystemItem _selectedLocalItem;
    private string _localCurrentPath;
    private FileStats _localFileStats;
    private FileStats _remoteFileStats;
    private string _port = "21";

    public string Port
    {
        get => _port;
        set => this.RaiseAndSetIfChanged(ref _port, value);
    }

    private bool _isConnected;

    public bool IsConnected
    {
        get => _isConnected;
        set => this.RaiseAndSetIfChanged(ref _isConnected, value);
    }

    private bool _isTransferring;

    public bool IsTransferring
    {
        get => _isTransferring;
        set => this.RaiseAndSetIfChanged(ref _isTransferring, value);
    }

    public DriveInfo SelectedDrive
    {
        get => _selectedDrive;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedDrive, value);
            if (value != null)
            {
                LocalCurrentPath = value.RootDirectory.FullName;
                RefreshLocalFiles();
            }
        }
    }

    public string LocalCurrentPath
    {
        get => _localCurrentPath;
        set => this.RaiseAndSetIfChanged(ref _localCurrentPath, value);
    }

    public FileSystemItem SelectedLocalItem
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

    private readonly ObservableCollection<BreadcrumbItem> _breadcrumbs;
    public ObservableCollection<BreadcrumbItem> Breadcrumbs => _breadcrumbs;
    public ReactiveCommand<NavigationItem, Unit> NavigateCommand { get; }
    private readonly ObservableCollection<FtpConnectionEntity> _recentConnections;
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

    public FileSystemItem SelectedFtpItem
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
        set => this.RaiseAndSetIfChanged(ref _localItems, value);
    }

    public ObservableCollection<string> LocalFiles
    {
        get => _localFiles;
        set => this.RaiseAndSetIfChanged(ref _localFiles, value);
    }

    public ObservableCollection<string> FtpFiles
    {
        get => _ftpFiles;
        set => this.RaiseAndSetIfChanged(ref _ftpFiles, value);
    }

    public ObservableCollection<FileSystemItem> FtpItems
    {
        get => _ftpItems;
        set => this.RaiseAndSetIfChanged(ref _ftpItems, value);
    }

    public ObservableCollection<DriveInfo> AvailableDrives
    {
        get => _availableDrives;
        set => this.RaiseAndSetIfChanged(ref _availableDrives, value);
    }

    public ReactiveCommand<FileSystemItem, Unit> NavigateToLocalDirectoryCommand { get; }
    public ReactiveCommand<Unit, Unit> NavigateLocalUpCommand { get; }
    public ReactiveCommand<Unit, Task> RefreshLocalCommand { get; }
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
    private FtpConnectionEntity _selectedRecentConnection;
    public ObservableCollection<FtpConnectionEntity> RecentConnections => _recentConnections;
    public ReactiveCommand<Unit, Unit> ShowRecentConnectionsCommand { get; }
    public ReactiveCommand<FtpConnectionEntity, Unit> DeleteConnectionCommand { get; }
    public ReactiveCommand<Unit, Unit> ShowMainViewCommand { get; }

    public FtpConnectionEntity SelectedRecentConnection
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
    
    public ReactiveCommand<Unit, Unit> ShowSettingsCommand { get; }
    private Control _currentView;
    private readonly Control _mainView;
    private readonly Control _settingsView;

    public Control CurrentView
    {
        get => _currentView;
        private set => this.RaiseAndSetIfChanged(ref _currentView, value);
    }
    
    private readonly IBackgroundService _backgroundService;
    private string _backgroundPath;

    public string BackgroundPath
    {
        get => _backgroundPath;
        private set => this.RaiseAndSetIfChanged(ref _backgroundPath, value);
    }

    public MainWindowViewModel(IFtpService ftpService, ISettingsService settingsService, IBackgroundService backgroundService)
    {
        _settingsService = settingsService;
        _ftpService = ftpService;
        _breadcrumbs = new ObservableCollection<BreadcrumbItem>();
        _recentConnections = new ObservableCollection<FtpConnectionEntity>();
        NavigateCommand = ReactiveCommand.CreateFromTask<NavigationItem>(NavigateToItemAsync);
        NavigateUpCommand = ReactiveCommand.CreateFromTask(NavigateUpAsync);
        NavigateToFolderCommand = ReactiveCommand.CreateFromTask<FileSystemItem>(NavigateToFolderAsync);
        SaveConnectionCommand = ReactiveCommand.CreateFromTask(SaveSuccessfulConnection);
        UploadCommand = ReactiveCommand.CreateFromTask(UploadFileAsync);
        BrowseCommand = ReactiveCommand.Create(BrowseFiles);
        DownloadCommand = ReactiveCommand.CreateFromTask(DownloadFileAsync);
        DeleteCommand = ReactiveCommand.CreateFromTask(DeleteFileAsync);
        RefreshCommand = ReactiveCommand.CreateFromTask(RefreshDirectoryAsync);
        NavigateLocalUpCommand = ReactiveCommand.Create(NavigateLocalUp);
        RefreshLocalCommand = ReactiveCommand.Create(RefreshLocalFiles);
        NavigateToLocalDirectoryCommand = ReactiveCommand.Create<FileSystemItem>(NavigateToLocalDirectory);
        ConnectCommand = ReactiveCommand.CreateFromTask(
            ConnectToFtpAsync,
            this.WhenAnyValue(x => x.IsConnected, connected => !connected));

        DisconnectCommand = ReactiveCommand.CreateFromTask(
            DisconnectAsync,
            this.WhenAnyValue(x => x.IsConnected));
        CleanCommand = ReactiveCommand.Create(CleanFields);
        LocalFiles = new ObservableCollection<string>();
        FtpFiles = new ObservableCollection<string>();
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
        ShowMainViewCommand = ReactiveCommand.Create(() => { });
        LocalFileStats = new FileStats();
        RemoteFileStats = new FileStats();

        
        var mainView = new MainView();
        var settingsView = new SettingsView();
    
        // Set DataContext for views
        mainView.DataContext = this;
        settingsView.DataContext = new SettingsWindowViewModel(settingsService, backgroundService); 
    
        // Initialize current view
        _mainView = mainView;
        _settingsView = settingsView;
        _currentView = mainView;

        ShowMainViewCommand = ReactiveCommand.Create(() => 
        {
            CurrentView = _mainView;
        });
    
        ShowSettingsCommand = ReactiveCommand.Create(() =>
        {
            CurrentView = _settingsView;
        });
        
        _backgroundService = backgroundService;
        BackgroundPath = backgroundService.CurrentBackground;
        
        _backgroundService.BackgroundChanged
            .Subscribe(path => BackgroundPath = path);

        LoadRecentConnections();
        InitializeLocalNavigation();
    }

    private void NavigateToLocalDirectory(FileSystemItem item)
    {
        if (item == null || !item.IsDirectory) return;

        LocalCurrentPath = item.FullPath;
        RefreshLocalFiles();
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

    private async void BrowseFiles()
    {
        var dialog = new OpenFolderDialog();
        var selectedPath =
            await dialog.ShowAsync(App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null);

        if (!string.IsNullOrEmpty(selectedPath))
        {
            SelectedPath = selectedPath;
            LocalFiles.Clear();
            foreach (var file in Directory.GetFiles(SelectedPath))
            {
                LocalFiles.Add(file);
            }
        }
    }

    private async Task ConnectToFtpAsync()
    {
        IsTransferring = true;
        try
        {
            StatusMessage = "Connecting to FTP server...";
            FtpItems?.Clear();
            UpdateRemoteStats();

            if (!int.TryParse(Port, out int portNumber))
            {
                StatusMessage = "Invalid port number";
                return;
            }

            if (!FtpAddress.StartsWith("ftp://", StringComparison.OrdinalIgnoreCase))
            {
                FtpAddress = $"ftp://{FtpAddress}";
            }

            var uri = new Uri(FtpAddress);
            var baseAddress = $"{uri.Scheme}://{uri.Host}";

            var configuration = new FtpConfiguration(
                baseAddress,
                Username,
                Password,
                portNumber,
                timeout: 10000);

            bool isConnected = await _ftpService.ConnectAsync(configuration);
            if (isConnected)
            {
                IsConnected = true;
                StatusMessage = "Connected successfully";
                var items = await _ftpService.ListDirectoryAsync(configuration);
                FtpItems = new ObservableCollection<FileSystemItem>(items);
                UpdateRemoteStats();

                await SaveSuccessfulConnection();
            }
            else
            {
                IsConnected = false;
                StatusMessage = "Failed to connect. Please check your credentials.";
            }
        }
        catch (Exception ex)
        {
            IsConnected = false;
            StatusMessage = $"Connection error: {ex.Message}";
            FtpItems?.Clear();
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
        try
        {
            if (SelectedLocalItem == null)
            {
                StatusMessage = "No item selected for upload";
                return;
            }

            var configuration = CreateConfiguration();
            var progress = new Progress<double>(p => TransferProgress = p);

            string targetDirectory = SelectedFtpItem?.IsDirectory == true
                ? SelectedFtpItem.FullPath
                : CurrentDirectory;

            if (SelectedLocalItem.IsDirectory)
            {
                await UploadDirectoryAsync(configuration, SelectedLocalItem, targetDirectory, progress);
            }
            else
            {
                var remotePath = Path.Combine(targetDirectory, SelectedLocalItem.Name).Replace('\\', '/');
                StatusMessage = $"Uploading {SelectedLocalItem.Name}...";
                await _ftpService.UploadFileAsync(configuration, SelectedLocalItem.FullPath, remotePath, progress);
            }

            await RefreshDirectoryAsync();
            StatusMessage = "Upload complete";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Upload failed: {ex.Message}";
        }
        finally
        {
            IsTransferring = false;
            TransferProgress = 0;
        }
    }

    private async Task UploadDirectoryAsync(FtpConfiguration config, FileSystemItem directory, string remotePath,
        IProgress<double> progress)
    {
        var targetPath = Path.Combine(remotePath, directory.Name).Replace('\\', '/');

        var createDirRequest =
            (FtpWebRequest)WebRequest.Create($"{config.FtpAddress.TrimEnd('/')}/{targetPath.TrimStart('/')}");
        createDirRequest.Method = WebRequestMethods.Ftp.MakeDirectory;
        createDirRequest.Credentials = new NetworkCredential(config.Username, config.Password);
        await createDirRequest.GetResponseAsync();

        var files = Directory.GetFiles(directory.FullPath);
        var dirs = Directory.GetDirectories(directory.FullPath);
        var totalItems = files.Length + dirs.Length;
        var currentItem = 0;

        foreach (var file in files)
        {
            var fileName = Path.GetFileName(file);
            var remoteFilePath = Path.Combine(targetPath, fileName).Replace('\\', '/');
            StatusMessage = $"Uploading {fileName}...";
            await _ftpService.UploadFileAsync(config, file, remoteFilePath, progress);
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
            await UploadDirectoryAsync(config, subDir, targetPath, progress);
            currentItem++;
            progress.Report((double)currentItem / totalItems * 100);
        }
    }

    private async Task DownloadFileAsync()
    {
        IsTransferring = true;
        try
        {
            if (SelectedFtpItem == null)
            {
                StatusMessage = "No item selected for download";
                return;
            }

            var configuration = CreateConfiguration();
            var progress = new Progress<double>(p => TransferProgress = p);

            if (SelectedFtpItem.IsDirectory)
            {
                await DownloadDirectoryAsync(configuration, SelectedFtpItem, LocalCurrentPath, progress);
            }
            else
            {
                var localPath = Path.Combine(LocalCurrentPath, SelectedFtpItem.Name);
                StatusMessage = $"Downloading {SelectedFtpItem.Name}...";
                await _ftpService.DownloadFileAsync(configuration, SelectedFtpItem.FullPath, localPath, progress);
            }

            await RefreshLocalFiles();
            StatusMessage = "Download complete";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Download failed: {ex.Message}";
        }
        finally
        {
            IsTransferring = false;
            TransferProgress = 0;
        }
    }

    private async Task DownloadDirectoryAsync(FtpConfiguration config, FileSystemItem directory, string localPath,
        IProgress<double> progress)
    {
        var targetPath = Path.Combine(localPath, directory.Name);
        Directory.CreateDirectory(targetPath);

        var items = await _ftpService.ListDirectoryAsync(config, directory.FullPath);
        var totalItems = items.Count();
        var currentItem = 0;

        foreach (var item in items)
        {
            if (item.IsDirectory)
            {
                await DownloadDirectoryAsync(config, item, targetPath, progress);
            }
            else
            {
                var itemPath = Path.Combine(targetPath, item.Name);
                StatusMessage = $"Downloading {item.Name}...";
                await _ftpService.DownloadFileAsync(config, item.FullPath, itemPath, progress);
            }

            currentItem++;
            progress.Report((double)currentItem / totalItems * 100);
        }
    }

    private async Task DeleteFileAsync()
    {
        IsTransferring = true;
        try
        {
            if (SelectedFtpItem == null) return;

            StatusMessage = $"Deleting {SelectedFtpItem.Name}...";
            var configuration = CreateConfiguration();
            await _ftpService.DeleteFileAsync(configuration, SelectedFtpItem.FullPath);
            await RefreshDirectoryAsync();
            StatusMessage = "Delete complete";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Delete failed: {ex.Message}";
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
            var items = await _ftpService.ListDirectoryAsync(configuration, CurrentDirectory);
            FtpItems.Clear();
            foreach (var item in items)
            {
                FtpItems.Add(item);
            }

            StatusMessage = "Directory refreshed";

            UpdateRemoteStats();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Refresh failed: {ex.Message}";
            FtpItems?.Clear();
            UpdateRemoteStats();
        }
    }

    private async Task RefreshLocalFiles()
    {
        try
        {
            var items = new List<FileSystemItem>();
            var currentDir = new DirectoryInfo(LocalCurrentPath);

            if (currentDir.Parent != null)
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
                items.Add(new FileSystemItem
                {
                    Name = dir.Name,
                    FullPath = dir.FullName,
                    IsDirectory = true,
                    Modified = dir.LastWriteTime,
                    Type = "Directory"
                });
            }

            foreach (var file in currentDir.GetFiles())
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

            LocalItems = new ObservableCollection<FileSystemItem>(items);
            UpdateLocalStats();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error accessing directory: {ex.Message}";
        }
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
        await RefreshDirectoryAsync();
    }

    private async Task NavigateToFolderAsync(FileSystemItem item)
    {
        if (!item.IsDirectory) return;
        CurrentDirectory = item.FullPath;
        UpdateBreadcrumbs();
        await RefreshDirectoryAsync();
    }

    private async Task NavigateToItemAsync(NavigationItem item)
    {
        CurrentDirectory = item.Path;
        UpdateBreadcrumbs();
        await RefreshDirectoryAsync();
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
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Saving successful connection: {FtpAddress}");
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
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Updating existing connection");
                connections.Remove(existing);
            }

            connections.Add(connection);
            var toSave = connections.OrderByDescending(c => c.LastUsed).Take(10).ToList();

            await _settingsService.SaveConnectionsAsync(toSave);
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Connection saved successfully");
            LoadRecentConnections();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Error saving connection: {ex.Message}");
        }
    }

    private async void LoadRecentConnections()
    {
        try
        {
            var connections = await _settingsService.LoadConnectionsAsync() ?? new List<FtpConnectionEntity>();
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

    private async Task NavigateToPathAsync(string path)
    {
        if (string.IsNullOrEmpty(path)) return;
        CurrentDirectory = path;
        UpdateBreadcrumbs();
        await RefreshDirectoryAsync();
    }

    private FtpConfiguration CreateConfiguration()
    {
        if (!int.TryParse(Port, out int portNumber))
        {
            throw new InvalidOperationException("Invalid port number");
        }

        var uri = new Uri(FtpAddress);
        var baseAddress = $"{uri.Scheme}://{uri.Host}";

        return new FtpConfiguration(
            baseAddress,
            Username,
            Password,
            portNumber,
            timeout: 5000);
    }

    private void InitializeLocalNavigation()
    {
        AvailableDrives = new ObservableCollection<DriveInfo>(DriveInfo.GetDrives());
        LocalItems = new ObservableCollection<FileSystemItem>();
        SelectedDrive = AvailableDrives.FirstOrDefault();
    }

    private void NavigateToLocalDirectory(string path)
    {
        LocalCurrentPath = path;
        RefreshLocalFiles();
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
        try
        {
            await _ftpService.DisconnectAsync();
            FtpItems?.Clear();
            UpdateRemoteStats();
            IsConnected = false;
            StatusMessage = "Disconnected from FTP server";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error disconnecting: {ex.Message}";
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
        try
        {
            if (IsConnected)
            {
                await DisconnectAsync();
            }

            FtpAddress = connection.Address;
            Username = connection.Username;
            Password = connection.Password;
            await ConnectToFtpAsync();
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
        try
        {
            await _settingsService.DeleteConnectionAsync(connection);
            _recentConnections.Remove(connection);
            StatusMessage = "Connection removed from history";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error removing connection: {ex.Message}";
        }
    }
}