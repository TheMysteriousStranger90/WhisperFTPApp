using System.Net;

namespace WhisperFTPApp.Extensions;

public static class IPAddressExtensions
{
    public static IPAddress GetNetworkAddress(this IPAddress address, IPAddress subnetMask)
    {
        ArgumentNullException.ThrowIfNull(address);
        ArgumentNullException.ThrowIfNull(subnetMask);

        byte[] ipBytes = address.GetAddressBytes();
        byte[] maskBytes = subnetMask.GetAddressBytes();
        byte[] networkBytes = new byte[ipBytes.Length];

        for (int i = 0; i < ipBytes.Length; i++)
        {
            networkBytes[i] = (byte)(ipBytes[i] & maskBytes[i]);
        }

        return new IPAddress(networkBytes);
    }

    public static int GetSubnetMaskLength(this IPAddress subnetMask)
    {
        ArgumentNullException.ThrowIfNull(subnetMask);

        var bits = subnetMask.GetAddressBytes()
            .Select(b => Convert.ToString(b, 2).Count(c => c == '1'))
            .Sum();
        return bits;
    }
}
