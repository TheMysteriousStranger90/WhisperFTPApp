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

    public static string GetDatabasePath() =>
        Path.Combine(AppDataDirectory, "DatabaseAzioWhisperFTP.db");

    public static string GetLogFilePath()
    {
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        return Path.Combine(AppDataDirectory, $"aziowhisperFTP_{timestamp}.log");
    }
}
