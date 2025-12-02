using Microsoft.Extensions.Logging;
using WhisperFTPApp.Configurations;

namespace WhisperFTPApp.Logger;

internal static class StaticFileLogger
{
    private static readonly Lazy<string> _filePath = new(() => PathManager.GetLogFilePath());
    private static readonly object _lock = new();

    private static bool IsEnabled { get; set; } = true;
    public static string LogFolderPath => PathManager.AppDataDirectory;

    private static void Log(LogLevel logLevel, string message)
    {
        if (!IsEnabled || logLevel == LogLevel.None) return;

        lock (_lock)
        {
            File.AppendAllText(_filePath.Value,
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}: {logLevel} - {message}\n");
        }
    }

    public static void LogInformation(string message) => Log(LogLevel.Information, message);
    public static void LogError(string message) => Log(LogLevel.Error, message);
    public static void LogWarning(string message) => Log(LogLevel.Warning, message);
}
