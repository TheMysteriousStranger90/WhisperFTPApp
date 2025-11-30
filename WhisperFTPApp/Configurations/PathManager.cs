using System.Globalization;

namespace WhisperFTPApp.Configurations;

internal static class PathManager
{
    private static readonly Lazy<string> _appDataDirectory = new(() =>
    {
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "AzioWhisper FTP");
        Directory.CreateDirectory(appDataPath);
        return appDataPath;
    });

    public static string AppDataDirectory => _appDataDirectory.Value;

    public static string GetDatabasePath()
    {
        var dataDirectory = Path.Combine(AppDataDirectory, "Data");
        Directory.CreateDirectory(dataDirectory);
        return Path.Combine(dataDirectory, "DatabaseAzioWhisperFTP.db");
    }

    public static string GetLogFilePath()
    {
        var logsDirectory = Path.Combine(AppDataDirectory, "Logs");
        Directory.CreateDirectory(logsDirectory);
        var timestamp = DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        return Path.Combine(logsDirectory, $"aziowhisperFTP_{timestamp}.log");
    }
}
