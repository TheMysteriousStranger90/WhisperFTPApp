using System.Globalization;

namespace WhisperFTPApp.Configurations;

internal static class PathManager
{
    private static readonly Lazy<string> _appDataDirectory = new(() =>
    {
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "WhisperFTP");
        Directory.CreateDirectory(appDataPath);
        return appDataPath;
    });

    public static string AppDataDirectory => _appDataDirectory.Value;

    public static string GetDatabasePath() =>
        Path.Combine(AppDataDirectory, "DatabaseWhisperFTPApp.db");

    public static string GetLogFilePath()
    {
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        return Path.Combine(AppDataDirectory, $"whisperFTP_{timestamp}.log");
    }
}
