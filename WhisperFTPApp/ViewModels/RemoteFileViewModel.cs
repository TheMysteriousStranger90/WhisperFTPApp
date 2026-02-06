using System.Collections.ObjectModel;
using System.Reactive;
using System.Windows.Input;
using ReactiveUI;
using WhisperFTPApp.Commands;
using WhisperFTPApp.Configurations;
using WhisperFTPApp.Logger;
using WhisperFTPApp.Models;
using WhisperFTPApp.Models.Navigations;
using WhisperFTPApp.Services.Interfaces;

namespace WhisperFTPApp.ViewModels;

public sealed class RemoteFileViewModel : ReactiveObject, IDisposable
{
    private readonly IFtpService _ftpService;
    private readonly Func<FtpConfiguration> _configurationFactory;

    private ObservableCollection<FileSystemItem> _ftpItems = new();
    private string _currentDirectory = "/";
    private FileSystemItem? _selectedFtpItem;
    private FileStats _remoteFileStats = new();
    private bool _isTransferring;
    private double _transferProgress;
    private readonly ObservableCollection<BreadcrumbItem> _breadcrumbs = new();
    private readonly ObservableCollection<FileSystemItem> _selectedFtpItems = new();

    public RemoteFileViewModel(IFtpService ftpService, Func<FtpConfiguration> configurationFactory)
    {
        _ftpService = ftpService;
        _configurationFactory = configurationFactory;

        NavigateCommand = ReactiveCommand.CreateFromTask<NavigationItem>(NavigateToItemAsync);
        NavigateUpCommand = ReactiveCommand.CreateFromTask(NavigateUpAsync);
        NavigateToFolderCommand = ReactiveCommand.CreateFromTask<FileSystemItem>(NavigateToFolderAsync);
        RefreshCommand = ReactiveCommand.CreateFromTask(RefreshDirectoryAsync);

        NavigateToPathCommand = new BreadcrumbNavigationCommand(path =>
        {
            if (!string.IsNullOrEmpty(path))
            {
                CurrentDirectory = path;
                UpdateBreadcrumbs();
                _ = RefreshDirectoryAsync();
            }
        });
    }

    public ObservableCollection<FileSystemItem> FtpItems
    {
        get => _ftpItems;
        private set => this.RaiseAndSetIfChanged(ref _ftpItems, value);
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

    public FileStats RemoteFileStats
    {
        get => _remoteFileStats;
        set => this.RaiseAndSetIfChanged(ref _remoteFileStats, value);
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

    public ObservableCollection<BreadcrumbItem> Breadcrumbs => _breadcrumbs;
    public ObservableCollection<FileSystemItem> SelectedFtpItems => _selectedFtpItems;

    public ReactiveCommand<NavigationItem, Unit> NavigateCommand { get; }
    public ReactiveCommand<Unit, Unit> NavigateUpCommand { get; }
    public ReactiveCommand<FileSystemItem, Unit> NavigateToFolderCommand { get; }
    public ReactiveCommand<Unit, Unit> RefreshCommand { get; }
    public ICommand NavigateToPathCommand { get; }

    public event Action<string>? StatusChanged;

    public void ClearItems()
    {
        _ftpItems.Clear();
        UpdateRemoteStats();
    }

    public async Task RefreshDirectoryAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            IsTransferring = true;
            StatusChanged?.Invoke("Refreshing directory...");
            TransferProgress = 0;

            var configuration = _configurationFactory();

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(30));

            var items = await _ftpService.ListDirectoryAsync(
                configuration,
                CurrentDirectory,
                cts.Token).ConfigureAwait(true);

            _ftpItems.Clear();

            var itemsList = items.ToList();
            int batchSize = 50;

            for (int i = 0; i < itemsList.Count; i += batchSize)
            {
                var batch = itemsList.Skip(i).Take(batchSize);
                foreach (var item in batch)
                {
                    _ftpItems.Add(item);
                }

                TransferProgress = (double)(i + batchSize) / itemsList.Count * 100;

                await Task.Delay(10, CancellationToken.None).ConfigureAwait(true);
            }

            StatusChanged?.Invoke($"Directory refreshed ({itemsList.Count} items)");
            UpdateRemoteStats();
        }
        catch (OperationCanceledException)
        {
            StatusChanged?.Invoke("Refresh timeout - server response too slow");
            _ftpItems.Clear();
            UpdateRemoteStats();
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke($"Refresh failed: {ex.Message}");
            _ftpItems.Clear();
            UpdateRemoteStats();
        }
        finally
        {
            IsTransferring = false;
            TransferProgress = 0;
        }
    }

    private async Task NavigateUpAsync(CancellationToken cancellationToken = default)
    {
        if (CurrentDirectory == "/") return;
        CurrentDirectory = Path.GetDirectoryName(CurrentDirectory)?.Replace('\\', '/') ?? "/";
        UpdateBreadcrumbs();
        await RefreshDirectoryAsync(cancellationToken).ConfigureAwait(true);
    }

    private async Task NavigateToFolderAsync(FileSystemItem? item, CancellationToken cancellationToken = default)
    {
        try
        {
            if (item == null)
            {
                StaticFileLogger.LogError("Navigation attempted with null item");
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
            await RefreshDirectoryAsync(cancellationToken).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            StaticFileLogger.LogError($"Navigation failed: {ex.Message}");
            StatusChanged?.Invoke($"Navigation failed: {ex.Message}");
        }
    }

    private async Task NavigateToItemAsync(NavigationItem item, CancellationToken cancellationToken = default)
    {
        CurrentDirectory = item.Path;
        UpdateBreadcrumbs();
        await RefreshDirectoryAsync(cancellationToken).ConfigureAwait(true);
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

    public void Dispose()
    {
        NavigateCommand.Dispose();
        NavigateUpCommand.Dispose();
        NavigateToFolderCommand.Dispose();
        RefreshCommand.Dispose();
    }
}
