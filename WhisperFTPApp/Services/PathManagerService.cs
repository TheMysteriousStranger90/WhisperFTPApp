using System.Globalization;
using WhisperFTPApp.Services.Interfaces;

namespace WhisperFTPApp.Services;

internal sealed class PathManagerService : IPathManager
{
    private readonly Lazy<string> _appDataDirectory = new(() =>
    {
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "AzioWhisperFTP");
        Directory.CreateDirectory(appDataPath);
        return appDataPath;
    });

    public string AppDataDirectory => _appDataDirectory.Value;

    public string GetDatabasePath()
    {
        var dataDirectory = Path.Combine(AppDataDirectory, "Data");
        Directory.CreateDirectory(dataDirectory);
        return Path.Combine(dataDirectory, "DatabaseAzioWhisperFTP.db");
    }

    public string GetLogFilePath()
    {
        var logsDirectory = Path.Combine(AppDataDirectory, "Logs");
        Directory.CreateDirectory(logsDirectory);
        var timestamp = DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        return Path.Combine(logsDirectory, $"aziowhisperFTP_{timestamp}.log");
    }

    public string GetSettingsFilePath()
    {
        var dataDirectory = Path.Combine(AppDataDirectory, "Data");
        Directory.CreateDirectory(dataDirectory);
        return Path.Combine(dataDirectory, "settings.json");
    }
}
