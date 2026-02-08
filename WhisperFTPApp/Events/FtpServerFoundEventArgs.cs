using WhisperFTPApp.Models;

namespace WhisperFTPApp.Events;

public sealed class FtpServerFoundEventArgs : EventArgs
{
    public WifiNetwork Network { get; }
    public FtpServerInfo ServerInfo { get; }

    public FtpServerFoundEventArgs(WifiNetwork network, FtpServerInfo serverInfo)
    {
        Network = network;
        ServerInfo = serverInfo;
    }
}
