using System.Globalization;

namespace WhisperFTPApp.Events;

internal sealed class CultureChangedEventArgs : EventArgs
{
    public CultureInfo Culture { get; }
    public CultureChangedEventArgs(CultureInfo culture) => Culture = culture;
}
