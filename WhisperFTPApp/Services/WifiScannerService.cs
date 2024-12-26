using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using NativeWifi;
using WhisperFTPApp.Logger;
using WhisperFTPApp.Models;
using WhisperFTPApp.Services.Interfaces;
using WhisperFTPApp.Utils;

namespace WhisperFTPApp.Services;

public class WifiScannerService : IWifiScannerService, IDisposable
{
    private readonly WlanClient _client;
    private bool _isScanning;

    public event EventHandler<WifiNetwork> NetworkFound;
    public event EventHandler<WifiNetwork> NetworkConnected;

    public WifiScannerService()
    {
        _client = new WlanClient();
    }

    public async Task<List<WifiNetwork>> ScanNetworksAsync(CancellationToken token)
    {
        var networks = new List<WifiNetwork>();
        _isScanning = true;

        try
        {
            while (_isScanning && !token.IsCancellationRequested)
            {
                foreach (WlanClient.WlanInterface wlanIface in _client.Interfaces)
                {
                    if (token.IsCancellationRequested) break;

                    try
                    {
                        wlanIface.Scan();
                        await Task.Delay(1000, token);

                        var wlanBssEntries = wlanIface.GetNetworkBssList();
                        foreach (var network in wlanBssEntries)
                        {
                            if (token.IsCancellationRequested) break;

                            var wifiNetwork = new WifiNetwork
                            {
                                SSID = NetworkUtils.GetStringForSSID(network.dot11Ssid),
                                BSSID = NetworkUtils.GetMacAddress(network.dot11Bssid),
                                SignalStrength = network.rssi,
                                Channel = NetworkUtils.GetChannelFromFrequency(network.chCenterFrequency),
                                SecurityType = NetworkUtils.GetSecurityType(wlanIface, network.dot11Ssid),
                                LastSeen = DateTime.Now
                            };

                            networks.Add(wifiNetwork);
                            NetworkFound?.Invoke(this, wifiNetwork);

                            if (wifiNetwork.SecurityType == "IEEE80211_Open")
                            {
                                var connected = await ConnectToNetworkAsync(wifiNetwork.SSID);
                                if (connected)
                                {
                                    var adapter = NetworkInterface.GetAllNetworkInterfaces()
                                        .FirstOrDefault(x => x.OperationalStatus == OperationalStatus.Up);
                                    wifiNetwork.IpAddress = NetworkUtils.GetLocalIPv4(adapter);
                                    wifiNetwork.HasOpenFtp = await CheckFtpAccessAsync(wifiNetwork.IpAddress);
                                    NetworkConnected?.Invoke(this, wifiNetwork);
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

                if (!token.IsCancellationRequested)
                {
                    await Task.Delay(5000, token);
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
            _isScanning = false;
        }

        return networks;
    }

    public async Task<bool> ConnectToNetworkAsync(string ssid)
    {
        try
        {
            foreach (WlanClient.WlanInterface wlanIface in _client.Interfaces)
            {
                var networks = wlanIface.GetAvailableNetworkList(0);
                var targetNetwork = networks.FirstOrDefault(n =>
                    NetworkUtils.GetStringForSSID(n.dot11Ssid) == ssid);

                if (!string.IsNullOrEmpty(NetworkUtils.GetStringForSSID(targetNetwork.dot11Ssid)))

                {
                    string profileName = ssid;
                    var connectionMode = Wlan.WlanConnectionMode.Profile;
                    var bssType = Wlan.Dot11BssType.Infrastructure;

                    wlanIface.Connect(connectionMode, bssType, profileName);
                    await Task.Delay(2000);

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

    public async Task<bool> CheckFtpAccessAsync(string ipAddress)
    {
        if (string.IsNullOrEmpty(ipAddress))
            return false;

        try
        {
            var ipParts = ipAddress.Split('.');
            var baseIP = $"{ipParts[0]}.{ipParts[1]}.{ipParts[2]}.";

            for (int i = 1; i < 255; i++)
            {
                var targetIP = baseIP + i;
                var (isOpen, status) = NetworkUtils.CheckFtpPort(targetIP);

                if (isOpen)
                {
                    StaticFileLogger.LogInformation($"Found open FTP port at: {targetIP}");
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            StaticFileLogger.LogError($"FTP scan error for {ipAddress}: {ex.Message}");
        }

        return false;
    }

    public void StopScan()
    {
        StaticFileLogger.LogInformation("Stopping WiFi scan");
        _isScanning = false;
    }

    protected virtual void OnNetworkFound(WifiNetwork network)
    {
        NetworkFound?.Invoke(this, network);
    }

    protected virtual void OnNetworkConnected(WifiNetwork network)
    {
        NetworkConnected?.Invoke(this, network);
    }

    public void Dispose()
    {
        StopScan();
        GC.SuppressFinalize(this);
    }
}