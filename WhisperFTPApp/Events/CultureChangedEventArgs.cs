using System.Globalization;

namespace WhisperFTPApp.Events;

public class CultureChangedEventArgs : EventArgs
{
    public CultureInfo Culture { get; }
    public CultureChangedEventArgs(CultureInfo culture) => Culture = culture;
}
