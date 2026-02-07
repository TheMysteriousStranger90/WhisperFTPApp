namespace WhisperFTPApp.Models;

public sealed class NetworkFoundEventArgs : EventArgs
{
    public WifiNetwork Network { get; }

    public NetworkFoundEventArgs(WifiNetwork network)
    {
        Network = network;
    }
}
