using System;

namespace WhisperFTPApp.Models;

public class WifiNetwork
{
    public string SSID { get; set; }
    public string BSSID { get; set; }
    public int SignalStrength { get; set; }
    public int Channel { get; set; }
    public string SecurityType { get; set; }
    public bool IsConnected { get; set; }
    public bool HasOpenFtp { get; set; }
    public string IpAddress { get; set; }
    public DateTime LastSeen { get; set; }
}