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
}
