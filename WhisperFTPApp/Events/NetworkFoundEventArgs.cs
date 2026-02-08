using WhisperFTPApp.Models;

namespace WhisperFTPApp.Events;

public sealed class NetworkFoundEventArgs : EventArgs
{
    public WifiNetwork Network { get; }

    public NetworkFoundEventArgs(WifiNetwork network)
    {
        Network = network;
    }
}
