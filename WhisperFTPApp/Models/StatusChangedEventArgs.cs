namespace WhisperFTPApp.Models;

public class StatusChangedEventArgs : EventArgs
{
    public string Message { get; }

    public StatusChangedEventArgs(string message)
    {
        Message = message;
    }
}
