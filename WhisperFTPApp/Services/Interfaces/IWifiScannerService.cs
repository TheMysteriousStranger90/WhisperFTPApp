using WhisperFTPApp.Models;

namespace WhisperFTPApp.Services.Interfaces;

public interface IWifiScannerService
{
    event EventHandler<NetworkFoundEventArgs>? NetworkFound;
    event EventHandler<NetworkConnectedEventArgs>? NetworkConnected;
    event EventHandler<FtpServerFoundEventArgs>? FtpServerFound;

    Task<List<WifiNetwork>> ScanNetworksAsync(CancellationToken token);
    Task<bool> ConnectToNetworkAsync(string ssid);
    void StopScan();
    void ClearScanCache();
}
