using WhisperFTPApp.Models;

namespace WhisperFTPApp.Events;

public sealed class NetworkConnectedEventArgs : EventArgs
{
    public WifiNetwork Network { get; }

    public NetworkConnectedEventArgs(WifiNetwork network)
    {
        Network = network;
    }
}
