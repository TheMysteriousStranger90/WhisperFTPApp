namespace WhisperFTPApp.Events;

public sealed class StatusChangedEventArgs : EventArgs
{
    public string Message { get; }
    public StatusChangedEventArgs(string message) => Message = message;
}
