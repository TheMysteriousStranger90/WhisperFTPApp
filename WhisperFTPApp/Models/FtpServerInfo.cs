namespace WhisperFTPApp.Models;

public class FtpServerInfo
{
    public required string IpAddress { get; set; }
    public int Port { get; set; } = 21;
    public bool IsAnonymousAllowed { get; set; }
    public string? ServerBanner { get; set; }
    public string? ServerType { get; set; }
    public TimeSpan ResponseTime { get; set; }
    public bool SupportsSSL { get; set; }
    public bool SupportsPasv { get; set; }
    public IList<string> SupportedFeatures { get; } = new List<string>();
    public DateTime DiscoveredAt { get; set; }
}
