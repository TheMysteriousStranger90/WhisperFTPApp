namespace WhisperFTPApp.Services.Interfaces;

public interface IPathManager
{
    string AppDataDirectory { get; }
    string GetDatabasePath();
    string GetLogFilePath();
}
