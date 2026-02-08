using System.Collections.ObjectModel;

namespace WhisperFTPApp.Models.Transfer;

public class TransferResult
{
    public int SuccessCount { get; set; }
    public int FailCount { get; set; }
    public int SkippedCount { get; set; }
    public Collection<string> FailedItems { get; } = new();
}
