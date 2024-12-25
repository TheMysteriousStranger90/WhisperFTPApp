using System;
using System.IO;
using Microsoft.Extensions.Logging;

namespace WhisperFTPApp.Logger;

public static class StaticFileLogger
{
    private static readonly Lazy<string> _filePath = new Lazy<string>(() =>
    {
        var logDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "WhisperFTP");
        Directory.CreateDirectory(logDirectory);
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        return Path.Combine(logDirectory, $"whisperFTP_{timestamp}.log");
    });

    private static readonly object _lock = new();
    public static bool IsEnabled { get; set; } = true;

    public static void Log(LogLevel logLevel, string message)
    {
        if (!IsEnabled || logLevel == LogLevel.None) return;

        lock (_lock)
        {
            File.AppendAllText(_filePath.Value, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}: {logLevel} - {message}\n");
        }
    }

    public static void LogInformation(string message) => Log(LogLevel.Information, message);
    public static void LogError(string message) => Log(LogLevel.Error, message);

    public static string GetLogFolderPath() => _filePath.Value;
}