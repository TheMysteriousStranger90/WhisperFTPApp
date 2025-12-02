using WhisperFTPApp.Enums;

namespace WhisperFTPApp.Models;

public class WifiNetwork
{
    public required string SSID { get; set; }
    public required string BSSID { get; set; }
    public int SignalStrength { get; set; }
    public int Channel { get; set; }
    public required string SecurityType { get; set; }
    public bool IsConnected { get; set; }
    public bool HasOpenFtp { get; set; }
    public required string IpAddress { get; set; }
    public DateTime LastSeen { get; set; }

    public string? FtpServerAddress { get; set; }
    public int FtpPort { get; set; } = 21;
    public bool FtpRequiresAuth { get; set; }
    public string? FtpBanner { get; set; }
    public string? FtpServerType { get; set; }
    public IList<int> OpenPorts { get; } = new List<int>();
    public TimeSpan FtpResponseTime { get; set; }
    public FtpScanStatus ScanStatus { get; set; } = FtpScanStatus.NotScanned;

    public string? Gateway { get; set; }
    public string? SubnetMask { get; set; }
    public int ConnectedDevices { get; set; }
    public NetworkSpeed Speed { get; set; }
}
