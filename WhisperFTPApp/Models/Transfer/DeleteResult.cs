using System.Collections.ObjectModel;

namespace WhisperFTPApp.Models.Transfer;

public class DeleteResult
{
    public int SuccessCount { get; set; }
    public int FailCount { get; set; }
    public Collection<string> FailedItems { get; } = new();
}
