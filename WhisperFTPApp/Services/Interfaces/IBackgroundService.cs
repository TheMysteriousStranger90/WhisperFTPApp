using System;
using System.Threading.Tasks;

namespace WhisperFTPApp.Services.Interfaces;

public interface IBackgroundService
{
    string CurrentBackground { get; }
    IObservable<string> BackgroundChanged { get; }
    Task ChangeBackgroundAsync(string path);
}