namespace WhisperFTPApp.Models;

public sealed class NetworkConnectedEventArgs : EventArgs
{
    public WifiNetwork Network { get; }

    public NetworkConnectedEventArgs(WifiNetwork network)
    {
        Network = network;
    }
}
