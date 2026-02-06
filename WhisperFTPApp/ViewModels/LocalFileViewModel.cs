using System.Collections.ObjectModel;
using System.Reactive;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using ReactiveUI;
using WhisperFTPApp.Logger;
using WhisperFTPApp.Models;

namespace WhisperFTPApp.ViewModels;

public sealed class LocalFileViewModel : ReactiveObject, IDisposable
{
    private ObservableCollection<FileSystemItem> _localItems = new();
    private ObservableCollection<DriveInfo> _availableDrives = new();
    private DriveInfo? _selectedDrive;
    private string _localCurrentPath = string.Empty;
    private FileSystemItem? _selectedLocalItem;
    private FileStats _localFileStats = new();
    private readonly ObservableCollection<FileSystemItem> _selectedLocalItems = new();

    public LocalFileViewModel()
    {
        NavigateLocalUpCommand = ReactiveCommand.Create(NavigateLocalUp);
        RefreshLocalCommand = ReactiveCommand.CreateFromTask(RefreshLocalFilesAsync);
        NavigateToLocalDirectoryCommand = ReactiveCommand.Create<FileSystemItem>(NavigateToLocalDirectory);
        BrowseCommand = ReactiveCommand.CreateFromTask<Control?>(BrowseFilesAsync);

        InitializeLocalNavigation();
    }

    public ObservableCollection<FileSystemItem> LocalItems
    {
        get => _localItems;
        private set => this.RaiseAndSetIfChanged(ref _localItems, value);
    }

    public ObservableCollection<DriveInfo> AvailableDrives
    {
        get => _availableDrives;
        private set => this.RaiseAndSetIfChanged(ref _availableDrives, value);
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
                    _ = RefreshLocalFilesAsync();
                    StatusChanged?.Invoke($"Selected drive: {value.Name}");
                }
                catch (Exception ex)
                {
                    StatusChanged?.Invoke($"Error accessing drive: {ex.Message}");
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

    public ObservableCollection<FileSystemItem> SelectedLocalItems => _selectedLocalItems;

    public ReactiveCommand<Unit, Unit> NavigateLocalUpCommand { get; }
    public ReactiveCommand<Unit, Unit> RefreshLocalCommand { get; }
    public ReactiveCommand<FileSystemItem, Unit> NavigateToLocalDirectoryCommand { get; }
    public ReactiveCommand<Control?, Unit> BrowseCommand { get; }

    public event Action<string>? StatusChanged;

    private void InitializeLocalNavigation()
    {
        AvailableDrives = new ObservableCollection<DriveInfo>(DriveInfo.GetDrives());
        LocalItems = new ObservableCollection<FileSystemItem>();
        SelectedDrive = AvailableDrives.FirstOrDefault();
    }

    private void NavigateToLocalDirectory(FileSystemItem? item)
    {
        if (item == null || !item.IsDirectory) return;
        LocalCurrentPath = item.FullPath;
        _ = RefreshLocalFilesAsync();
    }

    private void NavigateToLocalDirectory(string path)
    {
        LocalCurrentPath = path;
        _ = RefreshLocalFilesAsync();
    }

    private void NavigateLocalUp()
    {
        var parent = Directory.GetParent(LocalCurrentPath);
        if (parent != null)
        {
            NavigateToLocalDirectory(parent.FullName);
        }
    }

    private async Task BrowseFilesAsync(Control? control, CancellationToken cancellationToken = default)
    {
        if (control == null) return;

        var topLevel = TopLevel.GetTopLevel(control);
        if (topLevel?.StorageProvider == null) return;

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select Folder",
            AllowMultiple = false
        }).ConfigureAwait(true);

        if (folders.Count > 0)
        {
            var selectedPath = folders[0].Path.LocalPath;
            LocalCurrentPath = selectedPath;
            await RefreshLocalFilesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public Task RefreshLocalFilesAsync(CancellationToken cancellationToken = default)
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
            StatusChanged?.Invoke($"Loaded {items.Count} items from {LocalCurrentPath}");
        }
        catch (UnauthorizedAccessException ex)
        {
            StatusChanged?.Invoke($"Access denied: {ex.Message}");
            StaticFileLogger.LogError($"Access denied: {ex.Message}");
            LocalItems = new ObservableCollection<FileSystemItem>();
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke($"Error accessing directory: {ex.Message}");
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

    public void Dispose()
    {
        NavigateLocalUpCommand.Dispose();
        RefreshLocalCommand.Dispose();
        NavigateToLocalDirectoryCommand.Dispose();
        BrowseCommand.Dispose();
    }
}
