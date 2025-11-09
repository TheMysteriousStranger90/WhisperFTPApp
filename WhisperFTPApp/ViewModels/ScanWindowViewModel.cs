using System.Collections.ObjectModel;
using System.Reactive;
using Avalonia.Threading;
using ReactiveUI;
using WhisperFTPApp.Logger;
using WhisperFTPApp.Models;
using WhisperFTPApp.Services.Interfaces;

namespace WhisperFTPApp.ViewModels;

public class ScanWindowViewModel : ReactiveObject, IDisposable
{
    private readonly IWifiScannerService _wifiScanner;
    private readonly ObservableCollection<WifiNetwork> _networks = new ObservableCollection<WifiNetwork>();
    private readonly ObservableCollection<WifiNetwork> _connectedNetworks = new ObservableCollection<WifiNetwork>();
    private bool _isScanning;
    private CancellationTokenSource _cts = new CancellationTokenSource();
    private string _statusMessage = string.Empty;
    private double _scanProgress;
    private bool _disposed;

    public ScanWindowViewModel(IWifiScannerService wifiScanner)
    {
        _wifiScanner = wifiScanner;

        StartScanCommand = ReactiveCommand.CreateFromTask(
            StartScanAsync,
            this.WhenAnyValue(x => x.IsScanning, scanning => !scanning));

        StopScanCommand = ReactiveCommand.Create(
            StopScan,
            this.WhenAnyValue(x => x.IsScanning));

        _wifiScanner.NetworkFound += (sender, e) => OnNetworkFound(e);
        _wifiScanner.NetworkConnected += (sender, e) => OnNetworkConnected(e);
    }

    public ReactiveCommand<Unit, Unit> StartScanCommand { get; }
    public ReactiveCommand<Unit, Unit> StopScanCommand { get; }

    public bool IsScanning
    {
        get => _isScanning;
        set => this.RaiseAndSetIfChanged(ref _isScanning, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
    }

    public double ScanProgress
    {
        get => _scanProgress;
        set => this.RaiseAndSetIfChanged(ref _scanProgress, value);
    }

    public ObservableCollection<WifiNetwork> Networks => _networks;
    public ObservableCollection<WifiNetwork> ConnectedNetworks => _connectedNetworks;

    private async Task StartScanAsync()
    {
        try
        {
            IsScanning = true;
            StatusMessage = "Starting WiFi scan...";
            Networks.Clear();
            ConnectedNetworks.Clear();

            _cts = new CancellationTokenSource();
            await _wifiScanner.ScanNetworksAsync(_cts.Token).ConfigureAwait(false);

            if (!_cts.Token.IsCancellationRequested)
            {
                StatusMessage = $"Scan complete. Found {Networks.Count} networks";
            }
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Scan cancelled";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Scan failed: {ex.Message}";
            StaticFileLogger.LogError($"WiFi scan error: {ex}");
        }
        finally
        {
            IsScanning = false;
            ScanProgress = 0;
        }
    }

    private void StopScan()
    {
        _cts?.Cancel();
        _wifiScanner.StopScan();
        IsScanning = false;

        var openNetworks = Networks.Count(n => n.SecurityType == "IEEE80211_Open");
        var ftpNetworks = ConnectedNetworks.Count(n => n.HasOpenFtp);

        StatusMessage = $"Scan stopped.\n" +
                        $"Total networks found: {Networks.Count}\n" +
                        $"Open networks: {openNetworks}\n" +
                        $"Networks with open FTP: {ftpNetworks}";
    }

    private void OnNetworkFound(NetworkFoundEventArgs e)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            var existing = Networks.FirstOrDefault(n => n.BSSID == e.Network.BSSID);
            if (existing != null)
            {
                var index = Networks.IndexOf(existing);
                Networks[index] = e.Network;
            }
            else
            {
                Networks.Add(e.Network);
            }
        });
    }

    private void OnNetworkConnected(NetworkConnectedEventArgs e)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            var existing = ConnectedNetworks.FirstOrDefault(n => n.BSSID == e.Network.BSSID);
            if (existing == null)
            {
                ConnectedNetworks.Add(e.Network);
            }
        });
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            StopScan();
            _cts?.Dispose();
        }

        _disposed = true;
    }
}
