using System.Globalization;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using NativeWifi;

namespace WhisperFTPApp.Utils;

internal static class NetworkUtils
{
    public static string GetStringForSSID(Wlan.Dot11Ssid ssid)
    {
        return Encoding.ASCII.GetString(ssid.SSID, 0, (int)ssid.SSIDLength);
    }

    public static string GetMacAddress(byte[] macAddr)
    {
        ArgumentNullException.ThrowIfNull(macAddr);
        var str = new string[macAddr.Length];
        for (int i = 0; i < macAddr.Length; i++)
        {
            str[i] = macAddr[i].ToString("x2", CultureInfo.InvariantCulture);
        }

        return string.Join(":", str);
    }

    public static string GetSecurityType(WlanClient.WlanInterface wlanIface, Wlan.Dot11Ssid ssid)
    {
        ArgumentNullException.ThrowIfNull(wlanIface);

        var networks = wlanIface.GetAvailableNetworkList(0)
            .Where(network => GetStringForSSID(network.dot11Ssid) == GetStringForSSID(ssid));

        foreach (var network in networks)
        {
            return network.dot11DefaultAuthAlgorithm.ToString();
        }

        return "IEEE80211_Open";
    }

    public static int GetChannelFromFrequency(uint frequency)
    {
        uint[] channelFrequencies24GHz =
        {
            2412000, 2417000, 2422000, 2427000, 2432000, 2437000, 2442000, 2447000, 2452000, 2457000, 2462000, 2467000,
            2472000, 2484000
        };

        for (int channel = 1; channel <= 14; channel++)
        {
            if (frequency == channelFrequencies24GHz[channel - 1])
            {
                return channel;
            }
        }

        return (int)((frequency - 5000000) / 5000);
    }

    public static string? GetLocalIPv4(NetworkInterface adapter)
    {
        ArgumentNullException.ThrowIfNull(adapter);
        IPInterfaceProperties properties = adapter.GetIPProperties();
        return properties.UnicastAddresses
            .Where(addr => addr.Address.AddressFamily == AddressFamily.InterNetwork)
            .Select(addr => addr.Address.ToString())
            .FirstOrDefault();
    }

    public static (bool isOpen, string status) CheckFtpPort(string ipAddress)
    {
        ArgumentNullException.ThrowIfNull(ipAddress);
        try
        {
            using var client = new TcpClient();
            if (client.ConnectAsync(ipAddress, 21).Wait(1000))
            {
                return (true, "Open");
            }
            return (false, "Closed");
        }
        catch (Exception)
        {
            return (false, "Filtered");
        }
    }
}
