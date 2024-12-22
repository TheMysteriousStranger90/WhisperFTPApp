using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace WhisperFTPApp.Models;

public class IpRange : IEquatable<IpRange>
{
    public IpRange()
    {
        Range = string.Empty;
    }

    public IpRange(string range)
    {
        Range = range;
    }

    public string Range { get; set; }

    public bool Equals(IpRange other)
    {
        return other != null && Range == other.Range;
    }

    public List<IPAddress> GenerateIPAddresses()
    {
        if (string.IsNullOrEmpty(Range))
        {
            throw new ArgumentException("IP Range cannot be null or empty");
        }

        return Range.Split(',')
            .SelectMany(range => GenerateIPAddressesForRange(range.Trim()))
            .ToList();
    }

    private IEnumerable<IPAddress> GenerateIPAddressesForRange(string ipRange)
    {
        string[] parts = ipRange.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 4)
        {
            throw new ArgumentException("Invalid IP Range format");
        }

        string networkId = string.Join(".", parts.Take(3));
        string[] lastPart = parts[3].Split('-');

        if (!int.TryParse(lastPart[0], out int start))
        {
            throw new ArgumentException("Invalid start range");
        }

        int end = start;
        if (lastPart.Length > 1)
        {
            if (!int.TryParse(lastPart[1], out end))
            {
                throw new ArgumentException("Invalid end range");
            }
        }

        if (start > end)
        {
            throw new ArgumentException("Start range must be less than or equal to end range");
        }

        return Enumerable.Range(start, end - start + 1).Select(i =>
        {
            if (IPAddress.TryParse($"{networkId}.{i}", out IPAddress address))
            {
                return address;
            }

            return IPAddress.None;
        });
    }

    public static List<List<IPAddress>> DivideAddressesIntoChunks(IEnumerable<IPAddress> addresses, int numberOfChunks)
    {
        if (addresses == null)
        {
            throw new ArgumentNullException(nameof(addresses));
        }

        if (numberOfChunks <= 0)
        {
            throw new ArgumentException("The number of chunks must be greater than 0");
        }

        var result = new List<List<IPAddress>>();

        int totalAddresses = addresses.Count();

        int baseChunkSize = totalAddresses / numberOfChunks;
        int remainder = totalAddresses % numberOfChunks;

        int startIndex = 0;
        foreach (var chunkIndex in Enumerable.Range(0, numberOfChunks))
        {
            int currentChunkSize = baseChunkSize + (remainder-- > 0 ? 1 : 0);
            var chunk = addresses.Skip(startIndex).Take(currentChunkSize).ToList();
            result.Add(chunk);
            startIndex += currentChunkSize;
        }

        return result;
    }
}