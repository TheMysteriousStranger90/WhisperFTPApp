using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WhisperFTPApp.Models;

namespace WhisperFTPApp.Services.Interfaces;

public interface IWifiScannerService
{
    Task<List<WifiNetwork>> ScanNetworksAsync(CancellationToken token);
    Task<bool> ConnectToNetworkAsync(string ssid);
    Task<bool> CheckFtpAccessAsync(string ipAddress);
    event EventHandler<WifiNetwork> NetworkFound;
    event EventHandler<WifiNetwork> NetworkConnected;
    void StopScan();
}