using System.Collections.ObjectModel;
using System.Globalization;
using System.Reactive;
using System.Reactive.Linq;
using Avalonia.Threading;
using ReactiveUI;
using WhisperFTPApp.Enums;
using WhisperFTPApp.Events;
using WhisperFTPApp.Logger;
using WhisperFTPApp.Models;
using WhisperFTPApp.Services;
using WhisperFTPApp.Services.Interfaces;

namespace WhisperFTPApp.ViewModels;

public sealed class ScanWindowViewModel : ReactiveObject, IDisposable
{
    private readonly IWifiScannerService _wifiScanner;
    private readonly ObservableCollection<WifiNetwork> _networks = [];
    private readonly ObservableCollection<WifiNetwork> _connectedNetworks = [];
    private readonly ObservableCollection<FtpServerInfo> _ftpServers = [];
    private bool _isScanning;
    private CancellationTokenSource? _cts;
    private string _statusMessage = "Ready. Hybrid scan: WiFi discovery + FTP scan (current network only)";
    private double _scanProgress;
    private bool _disposed;
    private WifiNetwork? _selectedNetwork;
    private FtpServerInfo? _selectedFtpServer;
    private int _foundNetworksCount;
    private int _scannedHostsCount;
    private int _foundFtpServersCount;
    private string _scanMode = "Hybrid (Safe)";

    private readonly EventHandler<NetworkFoundEventArgs> _networkFoundHandler;
    private readonly EventHandler<NetworkConnectedEventArgs> _networkConnectedHandler;
    private readonly EventHandler<FtpServerFoundEventArgs> _ftpServerFoundHandler;

    public ScanWindowViewModel(IWifiScannerService wifiScanner)
    {
        _wifiScanner = wifiScanner ?? throw new ArgumentNullException(nameof(wifiScanner));

        _networkFoundHandler = (_, e) => OnNetworkFound(e);
        _networkConnectedHandler = (_, e) => OnNetworkConnected(e);
        _ftpServerFoundHandler = (_, e) => OnFtpServerFound(e);

        _wifiScanner.NetworkFound += _networkFoundHandler;
        _wifiScanner.NetworkConnected += _networkConnectedHandler;
        _wifiScanner.FtpServerFound += _ftpServerFoundHandler;

        StartScanCommand = ReactiveCommand.CreateFromTask(
            StartScanAsync,
            this.WhenAnyValue(x => x.IsScanning, scanning => !scanning));

        StopScanCommand = ReactiveCommand.Create(
            StopScan,
            this.WhenAnyValue(x => x.IsScanning));

        var canConnectToFtp = this.WhenAnyValue(x => x.SelectedFtpServer)
            .Select(server => server != null);

        ConnectToFtpServerCommand = ReactiveCommand.Create(
            ConnectToFtpServer,
            canConnectToFtp);

        ExportResultsCommand = ReactiveCommand.CreateFromTask(ExportResultsAsync);
        ClearResultsCommand = ReactiveCommand.Create(ClearResults);
        RefreshNetworkListCommand = ReactiveCommand.Create(RefreshNetworkList);
    }

    public ReactiveCommand<Unit, Unit> StartScanCommand { get; }
    public ReactiveCommand<Unit, Unit> StopScanCommand { get; }
    public ReactiveCommand<Unit, Unit> ConnectToFtpServerCommand { get; }
    public ReactiveCommand<Unit, Unit> ExportResultsCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearResultsCommand { get; }
    public ReactiveCommand<Unit, Unit> RefreshNetworkListCommand { get; }

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

    public string ScanMode
    {
        get => _scanMode;
        set => this.RaiseAndSetIfChanged(ref _scanMode, value);
    }

    public WifiNetwork? SelectedNetwork
    {
        get => _selectedNetwork;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedNetwork, value);
            UpdateSelectedNetworkStatus();
        }
    }

    public FtpServerInfo? SelectedFtpServer
    {
        get => _selectedFtpServer;
        set => this.RaiseAndSetIfChanged(ref _selectedFtpServer, value);
    }

    public int FoundNetworksCount
    {
        get => _foundNetworksCount;
        set => this.RaiseAndSetIfChanged(ref _foundNetworksCount, value);
    }

    public int ScannedHostsCount
    {
        get => _scannedHostsCount;
        set => this.RaiseAndSetIfChanged(ref _scannedHostsCount, value);
    }

    public int FoundFtpServersCount
    {
        get => _foundFtpServersCount;
        set => this.RaiseAndSetIfChanged(ref _foundFtpServersCount, value);
    }

    public ObservableCollection<WifiNetwork> Networks => _networks;
    public ObservableCollection<WifiNetwork> ConnectedNetworks => _connectedNetworks;
    public ObservableCollection<FtpServerInfo> FtpServers => _ftpServers;

    private async Task StartScanAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed) return;

        try
        {
            IsScanning = true;
            StatusMessage = "Starting HYBRID scan...\n" +
                            "• Discovering WiFi networks\n" +
                            "• Scanning FTP in current network only\n" +
                            "• Using smart cache to avoid re-scanning\n" +
                            "Your connection remains stable.";

            ClearCollections();

            _cts?.Dispose();
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            await _wifiScanner.ScanNetworksAsync(_cts.Token).ConfigureAwait(false);

            if (_cts is { Token.IsCancellationRequested: false })
            {
                await UpdateFinalStatusAsync().ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Scan cancelled. Your connection remained stable.";
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

    private void ClearCollections()
    {
        Networks.Clear();
        ConnectedNetworks.Clear();
        FtpServers.Clear();
        FoundNetworksCount = 0;
        ScannedHostsCount = 0;
        FoundFtpServersCount = 0;
    }

    private async Task UpdateFinalStatusAsync()
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var openNetworks = Networks.Count(n => n.SecurityType == "IEEE80211_Open");
            var requiresConnection = Networks.Count(n => n.ScanStatus == FtpScanStatus.RequiresConnection);

            StatusMessage = $"Hybrid scan complete!\n" +
                            $"━━━━━━━━━━━━━━━━━━━━\n" +
                            $"Networks found: {Networks.Count} (Open: {openNetworks})\n" +
                            $"FTP servers: {FtpServers.Count}\n" +
                            $"Require connection: {requiresConnection}\n" +
                            $"Your connection: stable";
        });
    }

    private void StopScan()
    {
        if (_disposed) return;

        _cts?.Cancel();
        _wifiScanner.StopScan();
        IsScanning = false;

        var openNetworks = Networks.Count(n => n.SecurityType == "IEEE80211_Open");
        var requiresConnection = Networks.Count(n => n.ScanStatus == FtpScanStatus.RequiresConnection);

        StatusMessage = $"Scan stopped.\n" +
                        $"━━━━━━━━━━━━━━━━━━━━\n" +
                        $"Results summary:\n" +
                        $"• Total networks: {Networks.Count}\n" +
                        $"• Open networks: {openNetworks}\n" +
                        $"• FTP servers found: {FtpServers.Count}\n" +
                        $"• Networks needing connection: {requiresConnection}\n" +
                        $"Your connection: stable";
    }

    private void ConnectToFtpServer()
    {
        if (SelectedFtpServer == null) return;

        StaticFileLogger.LogInformation(
            $"FTP server selected: {SelectedFtpServer.IpAddress}:{SelectedFtpServer.Port}");

        var anonymousInfo = SelectedFtpServer.IsAnonymousAllowed
            ? "Anonymous access available"
            : "Authentication required";

        var sslInfo = SelectedFtpServer.SupportsSSL ? "Supported" : "Not supported";

        StatusMessage = $"Selected FTP Server Info:\n" +
                        $"━━━━━━━━━━━━━━━━━━━━\n" +
                        $"Address: {SelectedFtpServer.IpAddress}:{SelectedFtpServer.Port}\n" +
                        $"Type: {SelectedFtpServer.ServerType}\n" +
                        $"Anonymous: {anonymousInfo}\n" +
                        $"SSL: {sslInfo}\n" +
                        $"Response: {SelectedFtpServer.ResponseTime.TotalMilliseconds:F0}ms\n" +
                        $"━━━━━━━━━━━━━━━━━━━━\n" +
                        $"Use this info in the Connection tab.";
    }

    private void UpdateSelectedNetworkStatus()
    {
        if (SelectedNetwork == null) return;

        var statusInfo = SelectedNetwork.ScanStatus switch
        {
            FtpScanStatus.Found => $"FTP Server: {SelectedNetwork.FtpServerAddress}",
            FtpScanStatus.NotFound => "No FTP servers found",
            FtpScanStatus.Scanning => "Scanning in progress...",
            FtpScanStatus.RequiresConnection => "Connect to scan for FTP",
            FtpScanStatus.Error => "Scan error occurred",
            _ => "Not scanned"
        };

        StatusMessage = $"Network: {SelectedNetwork.SSID}\n" +
                        $"Signal: {SelectedNetwork.SignalStrength}dBm\n" +
                        $"Security: {SelectedNetwork.SecurityType}\n" +
                        $"Status: {statusInfo}";
    }

    private async Task ExportResultsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            var filename = $"wifi_hybrid_scan_{timestamp}.txt";
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), filename);

            await using var writer = new StreamWriter(path);

            await WriteHeaderAsync(writer).ConfigureAwait(false);
            await WriteStatisticsAsync(writer).ConfigureAwait(false);
            await WriteFtpServersAsync(writer).ConfigureAwait(false);
            await WriteNetworksAsync(writer).ConfigureAwait(false);

            StatusMessage = $"Results exported to Desktop:\n{filename}";
            StaticFileLogger.LogInformation($"Scan results exported to {path}");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Export failed: {ex.Message}";
            StaticFileLogger.LogError($"Export error: {ex.Message}");
        }
    }

    private static async Task WriteHeaderAsync(StreamWriter writer)
    {
        await writer.WriteLineAsync("WiFi HYBRID Scan Results").ConfigureAwait(false);
        await writer.WriteLineAsync($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}").ConfigureAwait(false);
        await writer.WriteLineAsync(new string('=', 70)).ConfigureAwait(false);
        await writer.WriteLineAsync().ConfigureAwait(false);
        await writer.WriteLineAsync("SCAN MODE: HYBRID (SMART CACHE)").ConfigureAwait(false);
        await writer.WriteLineAsync("• WiFi networks: Passive discovery").ConfigureAwait(false);
        await writer.WriteLineAsync("• FTP scanning: Active (current network only)").ConfigureAwait(false);
        await writer.WriteLineAsync("• Cache: Prevents redundant scans of same networks").ConfigureAwait(false);
        await writer.WriteLineAsync("• Connection: Remained stable throughout scan").ConfigureAwait(false);
        await writer.WriteLineAsync().ConfigureAwait(false);
    }

    private async Task WriteStatisticsAsync(StreamWriter writer)
    {
        await writer.WriteLineAsync(new string('=', 70)).ConfigureAwait(false);
        await writer.WriteLineAsync("STATISTICS:").ConfigureAwait(false);
        await writer.WriteLineAsync(new string('-', 70)).ConfigureAwait(false);
        await writer.WriteLineAsync($"Total Networks Found: {Networks.Count}").ConfigureAwait(false);
        await writer.WriteLineAsync($"Open Networks: {Networks.Count(n => n.SecurityType == "IEEE80211_Open")}")
            .ConfigureAwait(false);
        await writer.WriteLineAsync($"FTP Servers Found: {FtpServers.Count}").ConfigureAwait(false);
        await writer.WriteLineAsync(
                $"Networks Requiring Connection: {Networks.Count(n => n.ScanStatus == FtpScanStatus.RequiresConnection)}")
            .ConfigureAwait(false);
        await writer.WriteLineAsync().ConfigureAwait(false);
    }

    private async Task WriteFtpServersAsync(StreamWriter writer)
    {
        if (FtpServers.Count == 0) return;

        await writer.WriteLineAsync(new string('=', 70)).ConfigureAwait(false);
        await writer.WriteLineAsync("FTP SERVERS FOUND (CURRENT NETWORK):").ConfigureAwait(false);
        await writer.WriteLineAsync(new string('-', 70)).ConfigureAwait(false);

        foreach (var server in FtpServers)
        {
            await writer.WriteLineAsync($"Server: {server.IpAddress}:{server.Port}").ConfigureAwait(false);
            await writer.WriteLineAsync($"  Type: {server.ServerType}").ConfigureAwait(false);
            await writer.WriteLineAsync($"  Anonymous Access: {server.IsAnonymousAllowed}").ConfigureAwait(false);
            await writer.WriteLineAsync($"  SSL Support: {server.SupportsSSL}").ConfigureAwait(false);
            await writer.WriteLineAsync($"  Response Time: {server.ResponseTime.TotalMilliseconds:F0}ms")
                .ConfigureAwait(false);
            await writer.WriteLineAsync($"  Banner: {server.ServerBanner}").ConfigureAwait(false);
            await writer.WriteLineAsync($"  Discovered: {server.DiscoveredAt:HH:mm:ss}").ConfigureAwait(false);
            await writer.WriteLineAsync().ConfigureAwait(false);
        }
    }

    private async Task WriteNetworksAsync(StreamWriter writer)
    {
        await writer.WriteLineAsync(new string('=', 70)).ConfigureAwait(false);
        await writer.WriteLineAsync("ALL DISCOVERED NETWORKS:").ConfigureAwait(false);
        await writer.WriteLineAsync(new string('-', 70)).ConfigureAwait(false);

        foreach (var network in Networks.OrderByDescending(n => n.SignalStrength))
        {
            await writer.WriteLineAsync($"SSID: {network.SSID}").ConfigureAwait(false);
            await writer.WriteLineAsync($"  BSSID: {network.BSSID}").ConfigureAwait(false);
            await writer.WriteLineAsync($"  Signal Strength: {network.SignalStrength} dBm").ConfigureAwait(false);
            await writer.WriteLineAsync($"  Channel: {network.Channel}").ConfigureAwait(false);
            await writer.WriteLineAsync($"  Security: {network.SecurityType}").ConfigureAwait(false);
            await writer.WriteLineAsync($"  FTP Scan Status: {network.ScanStatus}").ConfigureAwait(false);

            if (network.HasOpenFtp)
            {
                await writer.WriteLineAsync($"  FTP Server: {network.FtpServerAddress}").ConfigureAwait(false);
                await writer.WriteLineAsync($"  FTP Type: {network.FtpServerType}").ConfigureAwait(false);
            }

            if (network.IsConnected)
            {
                await writer.WriteLineAsync("  >>> CURRENT CONNECTION <<<").ConfigureAwait(false);
            }

            await writer.WriteLineAsync().ConfigureAwait(false);
        }
    }

    private void ClearResults()
    {
        if (_disposed) return;

        ClearCollections();

        if (_wifiScanner is WifiScannerService service)
        {
            service.ClearScanCache();
        }

        StatusMessage = "Results and cache cleared. Ready for new scan.";
    }

    private void RefreshNetworkList()
    {
        this.RaisePropertyChanged(nameof(Networks));
        this.RaisePropertyChanged(nameof(ConnectedNetworks));
        this.RaisePropertyChanged(nameof(FtpServers));
    }

    private void OnNetworkFound(NetworkFoundEventArgs e)
    {
        if (_disposed) return;

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
                FoundNetworksCount = Networks.Count;
            }

            UpdateScanningStatus(e.Network);
        });
    }

    private void UpdateScanningStatus(WifiNetwork network)
    {
        if (network.IsConnected)
        {
            StatusMessage = $"Scanning YOUR network: {network.SSID}\n" +
                            $"IP: {network.IpAddress}\n" +
                            $"Checking for FTP servers...";
        }
        else if (network.SecurityType == "IEEE80211_Open" &&
                 network.ScanStatus == FtpScanStatus.RequiresConnection)
        {
            StatusMessage = $"Found open network: {network.SSID}\n" +
                            "FTP scan not available (not connected)";
        }
    }

    private void OnNetworkConnected(NetworkConnectedEventArgs e)
    {
        if (_disposed) return;

        Dispatcher.UIThread.InvokeAsync(() =>
        {
            var existing = ConnectedNetworks.FirstOrDefault(n => n.BSSID == e.Network.BSSID);
            if (existing == null)
            {
                ConnectedNetworks.Add(e.Network);
                StaticFileLogger.LogInformation($"Added current network to ConnectedNetworks: {e.Network.SSID}");
            }
            else
            {
                var index = ConnectedNetworks.IndexOf(existing);
                ConnectedNetworks[index] = e.Network;
                StaticFileLogger.LogInformation($"Updated current network in ConnectedNetworks: {e.Network.SSID}");
            }
        });
    }

    private void OnFtpServerFound(FtpServerFoundEventArgs e)
    {
        if (_disposed) return;

        Dispatcher.UIThread.InvokeAsync(() =>
        {
            var existing = FtpServers.FirstOrDefault(s =>
                s.IpAddress == e.ServerInfo.IpAddress && s.Port == e.ServerInfo.Port);

            if (existing != null) return;

            FtpServers.Add(e.ServerInfo);
            FoundFtpServersCount = FtpServers.Count;
            ScannedHostsCount++;
            ScanProgress = Math.Min((double)ScannedHostsCount / 254 * 100, 100);

            var anonymousText = e.ServerInfo.IsAnonymousAllowed ? "Anonymous" : "Auth required";
            StatusMessage = $"FTP SERVER FOUND!\n" +
                            $"━━━━━━━━━━━━━━━━━━━━\n" +
                            $"{e.ServerInfo.IpAddress}:{e.ServerInfo.Port}\n" +
                            $"{e.ServerInfo.ServerType}\n" +
                            $"{anonymousText}\n" +
                            $"{e.ServerInfo.ResponseTime.TotalMilliseconds:F0}ms\n" +
                            $"Scanned: {ScannedHostsCount}/254 hosts";
        });
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            _wifiScanner.NetworkFound -= _networkFoundHandler;
            _wifiScanner.NetworkConnected -= _networkConnectedHandler;
            _wifiScanner.FtpServerFound -= _ftpServerFoundHandler;

            StopScan();

            _cts?.Dispose();
            _cts = null;

            StartScanCommand.Dispose();
            StopScanCommand.Dispose();
            ConnectToFtpServerCommand.Dispose();
            ExportResultsCommand.Dispose();
            ClearResultsCommand.Dispose();
            RefreshNetworkListCommand.Dispose();
        }

        _disposed = true;
    }
}
