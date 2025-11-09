using System.Diagnostics;
using System.Net.NetworkInformation;
using NativeWifi;
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

    public event EventHandler<NetworkFoundEventArgs>? NetworkFound;
    public event EventHandler<NetworkConnectedEventArgs>? NetworkConnected;

    public WifiScannerService()
    {
        _client = new WlanClient();
    }

    public async Task<List<WifiNetwork>> ScanNetworksAsync(CancellationToken token)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var networks = new List<WifiNetwork>();
        _scanCancellationSource?.Dispose();
        _scanCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(token);
        var scanStartTime = Stopwatch.GetTimestamp();

        StaticFileLogger.LogInformation($"Starting WiFi scan at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

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

                            var wifiNetwork = new WifiNetwork
                            {
                                SSID = NetworkUtils.GetStringForSSID(network.dot11Ssid),
                                BSSID = NetworkUtils.GetMacAddress(network.dot11Bssid),
                                SignalStrength = network.rssi,
                                Channel = NetworkUtils.GetChannelFromFrequency(network.chCenterFrequency),
                                SecurityType = NetworkUtils.GetSecurityType(wlanIface, network.dot11Ssid),
                                LastSeen = DateTime.Now,
                                IpAddress = string.Empty
                            };

                            networks.Add(wifiNetwork);
                            NetworkFound?.Invoke(this, new NetworkFoundEventArgs(wifiNetwork));

                            StaticFileLogger.LogInformation(
                                $"Network found: SSID={wifiNetwork.SSID}, " +
                                $"BSSID={wifiNetwork.BSSID}, " +
                                $"Signal={wifiNetwork.SignalStrength}dBm, " +
                                $"Channel={wifiNetwork.Channel}, " +
                                $"Security={wifiNetwork.SecurityType}");

                            if (wifiNetwork.SecurityType == "IEEE80211_Open")
                            {
                                StaticFileLogger.LogInformation(
                                    $"Attempting to connect to open network: {wifiNetwork.SSID}");
                                var connected = await ConnectToNetworkAsync(wifiNetwork.SSID).ConfigureAwait(false);
                                if (connected)
                                {
                                    var adapter = NetworkInterface.GetAllNetworkInterfaces()
                                        .FirstOrDefault(x => x.OperationalStatus == OperationalStatus.Up);

                                    if (adapter != null)
                                    {
                                        var ipAddress = NetworkUtils.GetLocalIPv4(adapter);
                                        if (!string.IsNullOrEmpty(ipAddress))
                                        {
                                            wifiNetwork.IpAddress = ipAddress;
                                            wifiNetwork.HasOpenFtp = await CheckFtpAccessAsync(ipAddress).ConfigureAwait(false);
                                            NetworkConnected?.Invoke(this, new NetworkConnectedEventArgs(wifiNetwork));

                                            StaticFileLogger.LogInformation(
                                                $"Connected to network: {wifiNetwork.SSID}, " +
                                                $"IP={wifiNetwork.IpAddress}, " +
                                                $"FTP={(wifiNetwork.HasOpenFtp ? "Open" : "Closed")}");
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        StaticFileLogger.LogInformation("Scan cancelled by user");
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

            StaticFileLogger.LogInformation(
                $"Scan completed in {scanDuration.TotalSeconds:F1} seconds\n" +
                $"Total networks found: {networks.Count}\n" +
                $"Open networks: {openNetworks}\n" +
                $"Networks with open FTP: {ftpNetworks}");
        }

        return networks;
    }

    public async Task<bool> ConnectToNetworkAsync(string ssid)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(ssid);

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
                    await Task.Delay(2000).ConfigureAwait(false);

                    return wlanIface.InterfaceState == Wlan.WlanInterfaceState.Connected &&
                           NetworkUtils.GetStringForSSID(
                               wlanIface.CurrentConnection.wlanAssociationAttributes.dot11Ssid) == ssid;
                }
            }
        }
        catch (Exception ex)
        {
            StaticFileLogger.LogError($"Failed to connect to network {ssid}: {ex.Message}");
        }

        return false;
    }

    public Task<bool> CheckFtpAccessAsync(string ipAddress)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(ipAddress);

        if (string.IsNullOrEmpty(ipAddress))
            return Task.FromResult(false);

        try
        {
            var ipParts = ipAddress.Split('.');
            if (ipParts.Length != 4)
                return Task.FromResult(false);

            var baseIP = $"{ipParts[0]}.{ipParts[1]}.{ipParts[2]}.";

            for (int i = 1; i < 255; i++)
            {
                var targetIP = baseIP + i;
                var (isOpen, _) = NetworkUtils.CheckFtpPort(targetIP);

                if (isOpen)
                {
                    StaticFileLogger.LogInformation($"Found open FTP port at: {targetIP}");
                    return Task.FromResult(true);
                }
            }
        }
        catch (Exception ex)
        {
            StaticFileLogger.LogError($"FTP scan error for {ipAddress}: {ex.Message}");
        }

        return Task.FromResult(false);
    }

    public void StopScan()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        StaticFileLogger.LogInformation("Stopping WiFi scan");
        _scanCancellationSource?.Cancel();
    }

    public void Dispose()
    {
        Dispose(true);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            StopScan();
            _scanCancellationSource?.Dispose();
            _scanCancellationSource = null;
        }

        _disposed = true;
    }
}
