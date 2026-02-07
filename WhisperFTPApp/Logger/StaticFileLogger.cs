using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using WhisperFTPApp.Services;

namespace WhisperFTPApp.Logger;

internal static class StaticFileLogger
{
    private static readonly Lazy<PathManagerService> _pathManager = new(() => new PathManagerService());
    private static readonly Lazy<string> _filePath = new(() => _pathManager.Value.GetLogFilePath());
    private static readonly SemaphoreSlim _writeLock = new(1, 1);
    private static readonly ConcurrentQueue<string> _logQueue = new();
    private static readonly Timer _flushTimer = new(FlushLogs, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));

    public static bool IsEnabled { get; set; } = true;

    public static string LogFolderPath => _pathManager.Value.AppDataDirectory;

    static StaticFileLogger()
    {
        AppDomain.CurrentDomain.ProcessExit += (_, _) => Flush();
        GC.KeepAlive(_flushTimer);
    }

    private static void Log(LogLevel logLevel, string message)
    {
        if (!IsEnabled || logLevel == LogLevel.None) return;

        var logEntry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{logLevel,-11}] {message}";
        _logQueue.Enqueue(logEntry);
    }

    private static void FlushLogs(object? state)
    {
        if (_logQueue.IsEmpty) return;

        if (!_writeLock.Wait(0)) return;

        try
        {
            var entries = new List<string>();
            while (_logQueue.TryDequeue(out var entry))
            {
                entries.Add(entry);
            }

            if (entries.Count > 0)
            {
                File.AppendAllLines(_filePath.Value, entries);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Logger error: {ex.Message}");
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public static void LogInformation(string message) => Log(LogLevel.Information, message);
    public static void LogError(string message) => Log(LogLevel.Error, message);
    public static void LogWarning(string message) => Log(LogLevel.Warning, message);
    public static void LogDebug(string message) => Log(LogLevel.Debug, message);
    public static void LogTrace(string message) => Log(LogLevel.Trace, message);

    public static void LogException(Exception ex, string? context = null)
    {
        var message = string.IsNullOrEmpty(context)
            ? $"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}"
            : $"{context} - {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}";
        Log(LogLevel.Error, message);
    }

    public static void Flush()
    {
        FlushLogs(null);
    }
}
