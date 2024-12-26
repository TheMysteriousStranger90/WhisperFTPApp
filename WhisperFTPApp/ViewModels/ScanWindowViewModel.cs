using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using ReactiveUI;
using WhisperFTPApp.Logger;
using WhisperFTPApp.Models;
using WhisperFTPApp.Services.Interfaces;

namespace WhisperFTPApp.ViewModels;

public class ScanWindowViewModel : ViewModelBase, IDisposable
{
    private readonly IWifiScannerService _wifiScanner;
    private ObservableCollection<WifiNetwork> _networks;
    private ObservableCollection<WifiNetwork> _connectedNetworks;
    private bool _isScanning;
    private CancellationTokenSource _cts;
    private string _statusMessage;
    private double _scanProgress;
    private bool _disposed;

    public ScanWindowViewModel(IWifiScannerService wifiScanner)
    {
        _wifiScanner = wifiScanner;
        _cts = new CancellationTokenSource();
        Networks = new ObservableCollection<WifiNetwork>();
        ConnectedNetworks = new ObservableCollection<WifiNetwork>();
        
        StartScanCommand = ReactiveCommand.CreateFromTask(
            StartScanAsync,
            this.WhenAnyValue(x => x.IsScanning, scanning => !scanning));
            
        StopScanCommand = ReactiveCommand.Create(
            StopScan,
            this.WhenAnyValue(x => x.IsScanning));
        
        _wifiScanner.NetworkFound += OnNetworkFound;
        _wifiScanner.NetworkConnected += OnNetworkConnected;
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

    private async Task StartScanAsync()
    {
        try
        {
            IsScanning = true;
            StatusMessage = "Starting WiFi scan...";
            Networks.Clear();
            ConnectedNetworks.Clear();
        
            _cts = new CancellationTokenSource();
            await _wifiScanner.ScanNetworksAsync(_cts.Token);
        
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

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            StopScan();
            _cts?.Dispose();
        }
        GC.SuppressFinalize(this);
    }
    
    public ObservableCollection<WifiNetwork> Networks
    {
        get => _networks;
        set => this.RaiseAndSetIfChanged(ref _networks, value);
    }

    public ObservableCollection<WifiNetwork> ConnectedNetworks
    {
        get => _connectedNetworks;
        set => this.RaiseAndSetIfChanged(ref _connectedNetworks, value);
    }

    private void OnNetworkFound(object sender, WifiNetwork network)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            var existing = Networks.FirstOrDefault(n => n.BSSID == network.BSSID);
            if (existing != null)
            {
                var index = Networks.IndexOf(existing);
                Networks[index] = network;
            }
            else
            {
                Networks.Add(network);
            }
        });
    }

    private void OnNetworkConnected(object sender, WifiNetwork network)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            var existing = ConnectedNetworks.FirstOrDefault(n => n.BSSID == network.BSSID);
            if (existing == null)
            {
                ConnectedNetworks.Add(network);
            }
        });
    }
}