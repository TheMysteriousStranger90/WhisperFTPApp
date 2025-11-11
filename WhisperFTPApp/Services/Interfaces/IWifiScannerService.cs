using WhisperFTPApp.Models;

namespace WhisperFTPApp.Services.Interfaces;

public interface IWifiScannerService
{
    Task<List<WifiNetwork>> ScanNetworksAsync(CancellationToken token);
    Task<bool> ConnectToNetworkAsync(string ssid);
    Task<bool> CheckFtpAccessAsync(string ipAddress);
    event EventHandler<NetworkFoundEventArgs> NetworkFound;
    event EventHandler<NetworkConnectedEventArgs> NetworkConnected;
    void StopScan();
}

public sealed class NetworkFoundEventArgs : EventArgs
{
    public WifiNetwork Network { get; }

    public NetworkFoundEventArgs(WifiNetwork network)
    {
        Network = network;
    }
}

public sealed class NetworkConnectedEventArgs : EventArgs
{
    public WifiNetwork Network { get; }

    public NetworkConnectedEventArgs(WifiNetwork network)
    {
        Network = network;
    }
}
