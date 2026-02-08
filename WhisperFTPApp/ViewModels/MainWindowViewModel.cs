using System.Reactive;
using Avalonia.Controls;
using ReactiveUI;
using WhisperFTPApp.Models;
using WhisperFTPApp.Models.Transfer;
using WhisperFTPApp.Services;
using WhisperFTPApp.Services.Interfaces;
using WhisperFTPApp.Views;

namespace WhisperFTPApp.ViewModels;

public sealed class MainWindowViewModel : ReactiveObject, IDisposable
{
    private readonly ConnectionViewModel _connectionViewModel;
    private readonly LocalFileViewModel _localFileViewModel;
    private readonly RemoteFileViewModel _remoteFileViewModel;
    private readonly TransferViewModel _transferViewModel;

    private Control _currentView;
    private readonly Control _mainView;
    private readonly Control _settingsView;
    private readonly Control _scanView;
    private string _backgroundPath = string.Empty;
    private string _statusMessage = string.Empty;

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

        _connectionViewModel = new ConnectionViewModel(ftpService, settingsService);
        _localFileViewModel = new LocalFileViewModel();
        _remoteFileViewModel = new RemoteFileViewModel(ftpService, () => _connectionViewModel.CreateConfiguration());
        _transferViewModel = new TransferViewModel(ftpService, () => _connectionViewModel.CreateConfiguration());

        _connectionViewModel.StatusChanged += (_, e) => StatusMessage = e.Message;
        _localFileViewModel.StatusChanged += (_, e) => StatusMessage = e.Message;
        _remoteFileViewModel.StatusChanged += (_, e) => StatusMessage = e.Message;
        _transferViewModel.StatusChanged += (_, e) => StatusMessage = e.Message;

        _connectionViewModel.ConnectionEstablished += async (_, _) =>
        {
            _remoteFileViewModel.ClearItems();
            await _remoteFileViewModel.RefreshDirectoryAsync().ConfigureAwait(false);
        };

        _connectionViewModel.ConnectionLost += (_, _) => { _remoteFileViewModel.ClearItems(); };

        UploadCommand = ReactiveCommand.CreateFromTask(UploadSelectedAsync);
        DownloadCommand = ReactiveCommand.CreateFromTask(DownloadSelectedAsync);
        DeleteCommand = ReactiveCommand.CreateFromTask(DeleteSelectedAsync);

        var localizationService = LocalizationService.Instance;

        var mainView = new MainView { DataContext = this };
        var settingsView = new SettingsView
        {
            DataContext = new SettingsWindowViewModel(
                settingsService,
                backgroundService,
                localizationService)
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
        backgroundService.BackgroundChanged.Subscribe(path => BackgroundPath = path);

        _ = backgroundService.InitializeAsync();
    }

    public ConnectionViewModel Connection => _connectionViewModel;
    public LocalFileViewModel LocalFiles => _localFileViewModel;
    public RemoteFileViewModel RemoteFiles => _remoteFileViewModel;
    public TransferViewModel Transfer => _transferViewModel;

    public string StatusMessage
    {
        get => _statusMessage;
        set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
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

    public ReactiveCommand<Unit, Unit> UploadCommand { get; }
    public ReactiveCommand<Unit, Unit> DownloadCommand { get; }
    public ReactiveCommand<Unit, Unit> DeleteCommand { get; }
    public ReactiveCommand<Unit, Unit> ShowMainViewCommand { get; }
    public ReactiveCommand<Unit, Unit> ShowScanViewCommand { get; }
    public ReactiveCommand<Unit, Unit> ShowSettingsCommand { get; }

    private async Task UploadSelectedAsync(CancellationToken cancellationToken = default)
    {
        List<FileSystemItem> itemsToUpload;

        if (_localFileViewModel.SelectedLocalItems.Count > 0)
        {
            itemsToUpload = _localFileViewModel.SelectedLocalItems
                .Where(i => !i.Name.Equals("..", StringComparison.Ordinal)).ToList();
        }
        else if (_localFileViewModel.SelectedLocalItem != null)
        {
            itemsToUpload = new List<FileSystemItem> { _localFileViewModel.SelectedLocalItem };
        }
        else
        {
            StatusMessage = "No items selected for upload";
            return;
        }

        if (itemsToUpload.Count == 0)
        {
            StatusMessage = "No items selected for upload";
            return;
        }

        var targetDirectory = _remoteFileViewModel.SelectedFtpItem?.IsDirectory == true
            ? _remoteFileViewModel.SelectedFtpItem.FullPath
            : _remoteFileViewModel.CurrentDirectory;

        var request = new TransferRequest(itemsToUpload, targetDirectory);

        await _transferViewModel.UploadAsync(request, cancellationToken).ConfigureAwait(true);
        await _remoteFileViewModel.RefreshDirectoryAsync(cancellationToken).ConfigureAwait(true);
    }

    private async Task DownloadSelectedAsync(CancellationToken cancellationToken = default)
    {
        List<FileSystemItem> itemsToDownload;

        if (_remoteFileViewModel.SelectedFtpItems.Count > 0)
        {
            itemsToDownload = _remoteFileViewModel.SelectedFtpItems.ToList();
        }
        else if (_remoteFileViewModel.SelectedFtpItem != null)
        {
            itemsToDownload = new List<FileSystemItem> { _remoteFileViewModel.SelectedFtpItem };
        }
        else
        {
            StatusMessage = "No items selected for download";
            return;
        }

        if (itemsToDownload.Count == 0)
        {
            StatusMessage = "No items selected for download";
            return;
        }

        var request = new TransferRequest(itemsToDownload, _localFileViewModel.LocalCurrentPath);

        await _transferViewModel.DownloadAsync(request, cancellationToken).ConfigureAwait(true);
        await _localFileViewModel.RefreshLocalFilesAsync(cancellationToken).ConfigureAwait(true);
    }

    private async Task DeleteSelectedAsync(CancellationToken cancellationToken = default)
    {
        List<FileSystemItem> itemsToDelete;

        if (_remoteFileViewModel.SelectedFtpItems.Count > 0)
        {
            itemsToDelete = _remoteFileViewModel.SelectedFtpItems.ToList();
        }
        else if (_remoteFileViewModel.SelectedFtpItem != null)
        {
            itemsToDelete = new List<FileSystemItem> { _remoteFileViewModel.SelectedFtpItem };
        }
        else
        {
            StatusMessage = "No items selected for deletion";
            return;
        }

        if (itemsToDelete.Count == 0)
        {
            StatusMessage = "No items selected for deletion";
            return;
        }

        var request = new DeleteRequest(itemsToDelete);

        await _transferViewModel.DeleteAsync(request, cancellationToken).ConfigureAwait(true);

        _remoteFileViewModel.SelectedFtpItems.Clear();
        await _remoteFileViewModel.RefreshDirectoryAsync(cancellationToken).ConfigureAwait(true);
    }

    public void Dispose()
    {
        _connectionViewModel.Dispose();
        _localFileViewModel.Dispose();
        _remoteFileViewModel.Dispose();
        _transferViewModel.Dispose();

        UploadCommand.Dispose();
        DownloadCommand.Dispose();
        DeleteCommand.Dispose();
        ShowMainViewCommand.Dispose();
        ShowScanViewCommand.Dispose();
        ShowSettingsCommand.Dispose();
    }
}
