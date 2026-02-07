using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using NativeWifi;
using WhisperFTPApp.Enums;
using WhisperFTPApp.Events;
using WhisperFTPApp.Logger;
using WhisperFTPApp.Models;
using WhisperFTPApp.Services.Interfaces;
using WhisperFTPApp.Utils;

namespace WhisperFTPApp.Services;

internal sealed class WifiScannerService : IWifiScannerService, IDisposable
{
    private readonly WlanClient _client;
    private CancellationTokenSource? _scanCancellationSource;
    private bool _disposed;
    private readonly SemaphoreSlim _ftpScanSemaphore = new(10);
    private string? _currentNetwork;
    private string? _currentIpAddress;

    private readonly Dictionary<string, WifiNetwork> _networkCache = new();
    private readonly object _networkCacheLock = new();
    private readonly HashSet<string> _scannedNetworks = new();
    private readonly List<Task> _backgroundTasks = new();

    public event EventHandler<NetworkFoundEventArgs>? NetworkFound;
    public event EventHandler<NetworkConnectedEventArgs>? NetworkConnected;
    public event EventHandler<FtpServerFoundEventArgs>? FtpServerFound;

    public WifiScannerService()
    {
        _client = new WlanClient();
        RememberCurrentNetwork();
    }

    public async Task<List<WifiNetwork>> ScanNetworksAsync(CancellationToken token)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var networks = new List<WifiNetwork>();
        _scanCancellationSource?.Dispose();
        _scanCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(token);
        var scanStartTime = Stopwatch.GetTimestamp();

        _backgroundTasks.Clear();

        StaticFileLogger.LogInformation($"Starting HYBRID WiFi scan at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        StaticFileLogger.LogInformation($"Current network: {_currentNetwork}, IP: {_currentIpAddress}");
        StaticFileLogger.LogInformation("Mode: Passive WiFi discovery + Active FTP scan (current network only)");

        try
        {
            while (!_scanCancellationSource.Token.IsCancellationRequested)
            {
                foreach (WlanClient.WlanInterface wlanIface in _client.Interfaces)
                {
                    if (_scanCancellationSource.Token.IsCancellationRequested) break;

                    try
                    {
                        StaticFileLogger.LogInformation($"Scanning interface: {wlanIface.InterfaceName}");
                        wlanIface.Scan();
                        await Task.Delay(1000, _scanCancellationSource.Token).ConfigureAwait(false);

                        var wlanBssEntries = wlanIface.GetNetworkBssList();
                        StaticFileLogger.LogInformation(
                            $"Found {wlanBssEntries.Length} networks on interface {wlanIface.InterfaceName}");

                        foreach (var network in wlanBssEntries)
                        {
                            if (_scanCancellationSource.Token.IsCancellationRequested) break;

                            var bssid = NetworkUtils.GetMacAddress(network.dot11Bssid);
                            var ssid = NetworkUtils.GetStringForSSID(network.dot11Ssid);

                            WifiNetwork wifiNetwork;

                            lock (_networkCacheLock)
                            {
                                if (_networkCache.TryGetValue(bssid, out var cachedNetwork))
                                {
                                    wifiNetwork = cachedNetwork;
                                    wifiNetwork.SignalStrength = network.rssi;
                                    wifiNetwork.LastSeen = DateTime.Now;
                                }
                                else
                                {
                                    wifiNetwork = new WifiNetwork
                                    {
                                        SSID = ssid,
                                        BSSID = bssid,
                                        SignalStrength = network.rssi,
                                        Channel = NetworkUtils.GetChannelFromFrequency(network.chCenterFrequency),
                                        SecurityType = NetworkUtils.GetSecurityType(wlanIface, network.dot11Ssid),
                                        LastSeen = DateTime.Now,
                                        IpAddress = string.Empty,
                                        ScanStatus = FtpScanStatus.NotScanned
                                    };
                                    _networkCache[bssid] = wifiNetwork;
                                }
                            }

                            var isCurrentNetwork = wifiNetwork.SSID == _currentNetwork;
                            if (isCurrentNetwork)
                            {
                                wifiNetwork.IsConnected = true;
                                wifiNetwork.IpAddress = _currentIpAddress ?? string.Empty;
                            }

                            var existing = networks.FirstOrDefault(n => n.BSSID == wifiNetwork.BSSID);
                            if (existing != null)
                            {
                                var index = networks.IndexOf(existing);
                                networks[index] = wifiNetwork;
                            }
                            else
                            {
                                networks.Add(wifiNetwork);
                            }

                            NetworkFound?.Invoke(this, new NetworkFoundEventArgs(wifiNetwork));

                            if (isCurrentNetwork)
                            {
                                NetworkConnected?.Invoke(this, new NetworkConnectedEventArgs(wifiNetwork));
                            }

                            StaticFileLogger.LogInformation(
                                $"Network found: SSID={wifiNetwork.SSID}, " +
                                $"BSSID={wifiNetwork.BSSID}, " +
                                $"Signal={wifiNetwork.SignalStrength}dBm, " +
                                $"Channel={wifiNetwork.Channel}, " +
                                $"Security={wifiNetwork.SecurityType}, " +
                                $"Current={isCurrentNetwork}");

                            var networkCacheKey = $"{wifiNetwork.SSID}_{wifiNetwork.IpAddress}";

                            if (isCurrentNetwork)
                            {
                                StaticFileLogger.LogInformation(
                                    $"✓ You are connected to network: {wifiNetwork.SSID} (Security: {wifiNetwork.SecurityType})");

                                if (_scannedNetworks.Contains(networkCacheKey))
                                {
                                    StaticFileLogger.LogInformation(
                                        $"Network {wifiNetwork.SSID} already scanned in this session - skipping FTP scan");
                                    continue;
                                }

                                StaticFileLogger.LogInformation(
                                    $"Starting FTP scan in YOUR current network...");

                                _scannedNetworks.Add(networkCacheKey);

                                var bgTask = Task.Run(async () =>
                                {
                                    try
                                    {
                                        await PopulateNetworkInfoAsync(wifiNetwork).ConfigureAwait(false);
                                        await ScanCurrentNetworkForFtpAsync(wifiNetwork,
                                                _scanCancellationSource.Token)
                                            .ConfigureAwait(false);

                                        NetworkFound?.Invoke(this, new NetworkFoundEventArgs(wifiNetwork));
                                    }
                                    catch (OperationCanceledException)
                                    {
                                        StaticFileLogger.LogInformation($"FTP scan cancelled for {wifiNetwork.SSID}");
                                    }
                                    catch (Exception ex)
                                    {
                                        StaticFileLogger.LogError($"FTP scan background error: {ex.Message}");
                                    }
                                }, _scanCancellationSource.Token);

                                _backgroundTasks.Add(bgTask);
                            }
                            else if (!isCurrentNetwork && wifiNetwork.SecurityType == "IEEE80211_Open")
                            {
                                wifiNetwork.ScanStatus = FtpScanStatus.RequiresConnection;
                                StaticFileLogger.LogInformation(
                                    $"Open network detected: {wifiNetwork.SSID} - requires manual connection for FTP scan");
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        StaticFileLogger.LogInformation("Scan cancelled by user");
                        await WaitForBackgroundTasksAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
                        return networks;
                    }
                    catch (Exception ex)
                    {
                        StaticFileLogger.LogError($"Error scanning interface: {ex.Message}");
                    }
                }

                if (!_scanCancellationSource.Token.IsCancellationRequested)
                {
                    await Task.Delay(5000, _scanCancellationSource.Token).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException)
        {
            StaticFileLogger.LogInformation("Scan cancelled by user");
            await WaitForBackgroundTasksAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            StaticFileLogger.LogError($"Scanning error: {ex.Message}");
            throw;
        }
        finally
        {
            var scanDuration = Stopwatch.GetElapsedTime(scanStartTime);
            var openNetworks = networks.Count(n => n.SecurityType == "IEEE80211_Open");
            var ftpNetworks = networks.Count(n => n.HasOpenFtp);
            var requireConnection = networks.Count(n => n.ScanStatus == FtpScanStatus.RequiresConnection);

            StaticFileLogger.LogInformation(
                $"Hybrid scan completed in {scanDuration.TotalSeconds:F1} seconds\n" +
                $"Total networks found: {networks.Count}\n" +
                $"Open networks: {openNetworks}\n" +
                $"Networks with FTP (scanned): {ftpNetworks}\n" +
                $"Networks requiring connection: {requireConnection}\n" +
                $"Your connection: {_currentNetwork} (stable)");
        }

        return networks;
    }

    private async Task WaitForBackgroundTasksAsync(TimeSpan timeout)
    {
        try
        {
            using var cts = new CancellationTokenSource(timeout);
            await Task.WhenAll(_backgroundTasks).WaitAsync(cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            StaticFileLogger.LogWarning("Background tasks did not complete within timeout");
        }
        catch (Exception ex)
        {
            StaticFileLogger.LogError($"Error waiting for background tasks: {ex.Message}");
        }
    }

    private async Task ScanCurrentNetworkForFtpAsync(WifiNetwork network, CancellationToken token)
    {
        if (string.IsNullOrEmpty(network.IpAddress))
        {
            StaticFileLogger.LogWarning($"No IP address for current network {network.SSID}");
            return;
        }

        network.ScanStatus = FtpScanStatus.Scanning;
        StaticFileLogger.LogInformation(
            $"Starting FTP scan for YOUR CURRENT network: {network.SSID} ({network.IpAddress})");

        try
        {
            var ipParts = network.IpAddress.Split('.');
            if (ipParts.Length != 4)
            {
                StaticFileLogger.LogError($"Invalid IP address format: {network.IpAddress}");
                network.ScanStatus = FtpScanStatus.Error;
                return;
            }

            var baseIP = $"{ipParts[0]}.{ipParts[1]}.{ipParts[2]}.";
            var scanTasks = new List<Task<FtpServerInfo?>>();

            var priorityIps = new[] { 1, 2, 254, 100, 200, 192, 168, 10, 20, 50 };
            foreach (var i in priorityIps)
            {
                if (token.IsCancellationRequested) break;
                scanTasks.Add(ScanSingleHostForFtpAsync(baseIP + i, token));
            }

            var priorityResults = await Task.WhenAll(scanTasks).ConfigureAwait(false);
            var foundServers = priorityResults.Where(r => r != null).ToList();

            if (foundServers.Count == 0)
            {
                StaticFileLogger.LogInformation("Priority IPs scan completed, starting full range scan...");
                scanTasks.Clear();

                for (int i = 1; i < 255; i++)
                {
                    if (token.IsCancellationRequested) break;
                    if (priorityIps.Contains(i)) continue;

                    var targetIP = baseIP + i;
                    scanTasks.Add(ScanSingleHostForFtpAsync(targetIP, token));
                }

                var allResults = await Task.WhenAll(scanTasks).ConfigureAwait(false);
                foundServers = allResults.Where(r => r != null).ToList();
            }

            if (foundServers.Count > 0)
            {
                network.HasOpenFtp = true;
                network.ScanStatus = FtpScanStatus.Found;
                network.ConnectedDevices = foundServers.Count;

                var mainServer = foundServers[0]!;
                network.FtpServerAddress = mainServer.IpAddress;
                network.FtpPort = mainServer.Port;
                network.FtpBanner = mainServer.ServerBanner;
                network.FtpServerType = mainServer.ServerType;
                network.FtpRequiresAuth = !mainServer.IsAnonymousAllowed;
                network.FtpResponseTime = mainServer.ResponseTime;

                StaticFileLogger.LogInformation(
                    $"✓✓✓ Found {foundServers.Count} FTP server(s) in YOUR network {network.SSID}\n" +
                    $"Main server: {mainServer.IpAddress}:{mainServer.Port}\n" +
                    $"Type: {mainServer.ServerType}\n" +
                    $"Banner: {mainServer.ServerBanner}\n" +
                    $"Anonymous: {mainServer.IsAnonymousAllowed}\n" +
                    $"SSL: {mainServer.SupportsSSL}");

                foreach (var server in foundServers)
                {
                    FtpServerFound?.Invoke(this, new FtpServerFoundEventArgs(network, server!));
                }
            }
            else
            {
                network.ScanStatus = FtpScanStatus.NotFound;
                StaticFileLogger.LogInformation($"No FTP servers found in your current network {network.SSID}");
            }
        }
        catch (OperationCanceledException)
        {
            network.ScanStatus = FtpScanStatus.NotScanned;
            throw;
        }
        catch (Exception ex)
        {
            network.ScanStatus = FtpScanStatus.Error;
            StaticFileLogger.LogError($"FTP scan error for network {network.SSID}: {ex.Message}");
        }
    }

    private async Task<FtpServerInfo?> ScanSingleHostForFtpAsync(
        string ipAddress,
        CancellationToken token)
    {
        try
        {
            await _ftpScanSemaphore.WaitAsync(token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return null;
        }

        try
        {
            var stopwatch = Stopwatch.StartNew();

            var (isOpen, banner) = await CheckFtpPortDetailedAsync(ipAddress, 21, token)
                .ConfigureAwait(false);

            if (!isOpen)
                return null;

            stopwatch.Stop();

            var serverInfo = new FtpServerInfo
            {
                IpAddress = ipAddress,
                Port = 21,
                ServerBanner = banner,
                ResponseTime = stopwatch.Elapsed,
                DiscoveredAt = DateTime.Now
            };

            serverInfo.ServerType = DetermineServerType(banner);
            serverInfo.IsAnonymousAllowed = await CheckAnonymousAccessAsync(ipAddress, token)
                .ConfigureAwait(false);
            serverInfo.SupportsSSL = await CheckFtpSslSupportAsync(ipAddress, token)
                .ConfigureAwait(false);

            StaticFileLogger.LogInformation(
                $"  FTP server detected at {ipAddress}\n" +
                $"  Type: {serverInfo.ServerType}\n" +
                $"  Anonymous: {serverInfo.IsAnonymousAllowed}\n" +
                $"  SSL: {serverInfo.SupportsSSL}\n" +
                $"  Response time: {serverInfo.ResponseTime.TotalMilliseconds:F0}ms");

            return serverInfo;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception)
        {
            return null;
        }
        finally
        {
            _ftpScanSemaphore.Release();
        }
    }

    private static async Task<(bool isOpen, string? banner)> CheckFtpPortDetailedAsync(
        string ipAddress,
        int port,
        CancellationToken token)
    {
        TcpClient? client = null;
        try
        {
            client = new TcpClient();

            using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(token);
            connectCts.CancelAfter(800);

            try
            {
                await client.ConnectAsync(ipAddress, port, connectCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return (false, null);
            }
            catch (SocketException)
            {
                return (false, null);
            }

            if (!client.Connected)
                return (false, null);

            using var stream = client.GetStream();
            using var reader = new StreamReader(stream);

            using var bannerCts = CancellationTokenSource.CreateLinkedTokenSource(token);
            bannerCts.CancelAfter(500);

            try
            {
                string? banner;
#if NET6_0_OR_GREATER
                banner = await reader.ReadLineAsync(bannerCts.Token).ConfigureAwait(false);
#else
                var readTask = reader.ReadLineAsync();
                banner = await readTask.ConfigureAwait(false);
#endif
                return (true, banner);
            }
            catch (OperationCanceledException)
            {
                return (true, null);
            }
        }
        catch
        {
            return (false, null);
        }
        finally
        {
            client?.Dispose();
        }
    }

    #pragma warning disable SYSLIB0014
    private static async Task<bool> CheckAnonymousAccessAsync(string ipAddress, CancellationToken token)
    {
        try
        {
            if (token.IsCancellationRequested) return false;

            var uri = new Uri($"ftp://{ipAddress}");
            var request = (FtpWebRequest)WebRequest.Create(uri);
            request.Method = WebRequestMethods.Ftp.ListDirectory;
            request.Credentials = new NetworkCredential("anonymous", "anonymous@example.com");
            request.Timeout = 2000;

            using var response = (FtpWebResponse)await request.GetResponseAsync().ConfigureAwait(false);
            return response.StatusCode == FtpStatusCode.OpeningData ||
                   response.StatusCode == FtpStatusCode.DataAlreadyOpen;
        }
        catch
        {
            return false;
        }
    }
    #pragma warning restore SYSLIB0014

    #pragma warning disable SYSLIB0014
    private static async Task<bool> CheckFtpSslSupportAsync(string ipAddress, CancellationToken token)
    {
        try
        {
            if (token.IsCancellationRequested) return false;

            var uri = new Uri($"ftp://{ipAddress}");
            var request = (FtpWebRequest)WebRequest.Create(uri);
            request.Method = WebRequestMethods.Ftp.ListDirectory;
            request.EnableSsl = true;
            request.Timeout = 1500;

            using var response = (FtpWebResponse)await request.GetResponseAsync().ConfigureAwait(false);
            return true;
        }
        catch
        {
            return false;
        }
    }
    #pragma warning restore SYSLIB0014

    private static string DetermineServerType(string? banner)
    {
        if (string.IsNullOrEmpty(banner))
            return "Unknown";

        banner = banner.ToUpperInvariant();

        if (banner.Contains("FILEZILLA", StringComparison.OrdinalIgnoreCase)) return "FileZilla Server";
        if (banner.Contains("PROFTPD", StringComparison.OrdinalIgnoreCase)) return "ProFTPD";
        if (banner.Contains("VSFTPD", StringComparison.OrdinalIgnoreCase)) return "vsftpd";
        if (banner.Contains("PURE-FTPD", StringComparison.OrdinalIgnoreCase)) return "Pure-FTPd";
        if (banner.Contains("MICROSOFT", StringComparison.OrdinalIgnoreCase)) return "Microsoft IIS FTP";
        if (banner.Contains("SERV-U", StringComparison.OrdinalIgnoreCase)) return "Serv-U FTP";
        if (banner.Contains("WU-FTPD", StringComparison.OrdinalIgnoreCase)) return "WU-FTPD";
        if (banner.Contains("OPENSSH", StringComparison.OrdinalIgnoreCase)) return "OpenSSH SFTP";

        return "Generic FTP Server";
    }

    private void RememberCurrentNetwork()
    {
        try
        {
            foreach (WlanClient.WlanInterface wlanIface in _client.Interfaces)
            {
                if (wlanIface.InterfaceState == Wlan.WlanInterfaceState.Connected)
                {
                    _currentNetwork = NetworkUtils.GetStringForSSID(
                        wlanIface.CurrentConnection.wlanAssociationAttributes.dot11Ssid);

                    var adapter = NetworkInterface.GetAllNetworkInterfaces()
                        .FirstOrDefault(x => x.OperationalStatus == OperationalStatus.Up &&
                                             x.NetworkInterfaceType == NetworkInterfaceType.Wireless80211);

                    if (adapter != null)
                    {
                        _currentIpAddress = NetworkUtils.GetLocalIPv4(adapter);
                    }

                    StaticFileLogger.LogInformation(
                        $"Current network remembered: {_currentNetwork}, IP: {_currentIpAddress}");
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            StaticFileLogger.LogError($"Failed to remember current network: {ex.Message}");
        }
    }

    private static async Task PopulateNetworkInfoAsync(WifiNetwork network)
    {
        try
        {
            var adapter = NetworkInterface.GetAllNetworkInterfaces()
                .FirstOrDefault(x => x.OperationalStatus == OperationalStatus.Up &&
                                     x.NetworkInterfaceType == NetworkInterfaceType.Wireless80211);

            if (adapter != null)
            {
                network.IpAddress = NetworkUtils.GetLocalIPv4(adapter)!;

                var ipProps = adapter.GetIPProperties();

                var gateway = ipProps.GatewayAddresses.FirstOrDefault();
                if (gateway != null)
                {
                    network.Gateway = gateway.Address.ToString();
                }

                var unicast = ipProps.UnicastAddresses
                    .FirstOrDefault(addr => addr.Address.AddressFamily == AddressFamily.InterNetwork);
                if (unicast != null)
                {
                    network.SubnetMask = unicast.IPv4Mask.ToString();
                }

                network.Speed = DetermineNetworkSpeed(adapter.Speed);

                StaticFileLogger.LogInformation(
                    $"Network info - IP: {network.IpAddress}, " +
                    $"Gateway: {network.Gateway}, " +
                    $"Speed: {network.Speed}");
            }
        }
        catch (Exception ex)
        {
            StaticFileLogger.LogError($"Failed to populate network info: {ex.Message}");
        }

        await Task.CompletedTask.ConfigureAwait(false);
    }

    private static NetworkSpeed DetermineNetworkSpeed(long speedBps)
    {
        var speedMbps = speedBps / 1_000_000.0;

        return speedMbps switch
        {
            < 1 => NetworkSpeed.Slow,
            < 10 => NetworkSpeed.Medium,
            < 100 => NetworkSpeed.Fast,
            _ => NetworkSpeed.VeryFast
        };
    }

    public async Task<bool> ConnectToNetworkAsync(string ssid)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(ssid);

        StaticFileLogger.LogWarning(
            $"Manual connection requested to network: {ssid}\n" +
            $"This will disconnect from current network: {_currentNetwork}");

        try
        {
            foreach (WlanClient.WlanInterface wlanIface in _client.Interfaces)
            {
                var networks = wlanIface.GetAvailableNetworkList(0);
                var targetNetwork = networks.FirstOrDefault(n =>
                    NetworkUtils.GetStringForSSID(n.dot11Ssid) == ssid);

                if (targetNetwork.dot11Ssid.SSID != null &&
                    !string.IsNullOrEmpty(NetworkUtils.GetStringForSSID(targetNetwork.dot11Ssid)))
                {
                    string profileName = ssid;
                    var connectionMode = Wlan.WlanConnectionMode.Profile;
                    var bssType = Wlan.Dot11BssType.Infrastructure;

                    wlanIface.Connect(connectionMode, bssType, profileName);
                    await Task.Delay(3000).ConfigureAwait(false);

                    var connected = wlanIface.InterfaceState == Wlan.WlanInterfaceState.Connected &&
                                    NetworkUtils.GetStringForSSID(
                                        wlanIface.CurrentConnection.wlanAssociationAttributes.dot11Ssid) == ssid;

                    if (connected)
                    {
                        StaticFileLogger.LogInformation($"✓ Successfully connected to {ssid}");
                        RememberCurrentNetwork();
                    }

                    return connected;
                }
            }
        }
        catch (Exception ex)
        {
            StaticFileLogger.LogError($"Failed to connect to network {ssid}: {ex.Message}");
        }

        return false;
    }

    [Obsolete("Use ScanCurrentNetworkForFtpAsync instead")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S1133:Deprecated code should be removed",
        Justification = "Kept for backward compatibility, will be removed in next major version")]
    public Task<bool> CheckFtpAccessAsync(string ipAddress)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(ipAddress);
        return Task.FromResult(false);
    }

    public void StopScan()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        StaticFileLogger.LogInformation("Stopping WiFi scan");
        _scanCancellationSource?.Cancel();
    }

    public void ClearScanCache()
    {
        _scannedNetworks.Clear();
        lock (_networkCacheLock)
        {
            _networkCache.Clear();
        }
        _backgroundTasks.Clear();
        StaticFileLogger.LogInformation("Scan cache cleared");
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
            StopScan();
            _scanCancellationSource?.Dispose();
            _scanCancellationSource = null;
            _ftpScanSemaphore?.Dispose();
            _scannedNetworks.Clear();
            _backgroundTasks.Clear();
            lock (_networkCacheLock)
            {
                _networkCache.Clear();
            }
        }

        _disposed = true;
    }
}
