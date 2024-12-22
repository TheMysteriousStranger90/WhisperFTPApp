using System;
using System.Net;
using System.Net.NetworkInformation;
using WhisperFTPApp.Enums;

namespace WhisperFTPApp.Models;

public class Device : IEquatable<Device>
    {
        public Device(IPAddress ipAddress)
        {
            Status = DeviceStatus.Unknown;
            Name = string.Empty;
            Ip = ipAddress;
            Manufacturer = string.Empty;
            MacAddress = PhysicalAddress.None;
            Comments = string.Empty;
            Favorite = false;
            ScannedTime = DateTime.UtcNow;
        }

        public Device(DeviceStatus status, string name, IPAddress ip, string manufactor, PhysicalAddress macAddress, 
            string comments, DateTime scannedTime, Service service = null)
        {
            Status = status;
            Name = name;
            Ip = ip;
            Manufacturer = manufactor;
            MacAddress = macAddress;
            Comments = comments;
            Favorite = false;
            ScannedTime = scannedTime;
            Service = service;
        }

        public Device(DeviceStatus status, string name, IPAddress ip, string manufactor, PhysicalAddress macAddress,
            string comments, bool favorite, DeviceType type, DateTime scannedTime, Service service = null)
        {
            Status = status;
            Name = name;
            Ip = ip;
            Manufacturer = manufactor;
            MacAddress = macAddress;
            Comments = comments;
            Favorite = favorite;
            Type = type;
            ScannedTime = scannedTime;
            Service = service;
        }

        public DeviceStatus Status { get; set; }
        public string Name { get; set; }
        public IPAddress Ip { get; set; }
        public string Manufacturer { get; set; }
        public PhysicalAddress MacAddress { get; set; }
        public string Comments { get; set; }
        public bool Favorite { get; private set; }
        public DateTime ScannedTime { get; }
        public Service Service { get; set; }
        public DeviceType Type { get; set; } = DeviceType.Undefined;

        public bool SupportHttp => Service != null;

        public bool Equals(Device other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;

            return Status == other.Status &&
                   Name == other.Name &&
                   Equals(Ip, other.Ip) &&
                   Manufacturer == other.Manufacturer &&
                   Equals(MacAddress, other.MacAddress) &&
                   Comments == other.Comments &&
                   Favorite == other.Favorite;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as Device);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + Status.GetHashCode();
                hash = hash * 23 + (Name?.GetHashCode() ?? 0);
                hash = hash * 23 + (Ip?.GetHashCode() ?? 0);
                hash = hash * 23 + (Manufacturer?.GetHashCode() ?? 0);
                hash = hash * 23 + (MacAddress?.GetHashCode() ?? 0);
                hash = hash * 23 + (Comments?.GetHashCode() ?? 0);
                hash = hash * 23 + Favorite.GetHashCode();
                return hash;
            }
        }

        public override string ToString()
        {
            return $"{Status}: {Name}; {Ip}; {Manufacturer}; {MacAddress}; {Comments}";
        }

        public bool MarkAsFavorite()
        {
            Favorite = true;
            return Favorite;
        }

        public bool UnmarkAsFavorite()
        {
            Favorite = false;
            return Favorite;
        }
    }