using System.Collections.ObjectModel;

namespace WhisperFTPApp.Models.Transfer;

public class TransferRequest
{
    public TransferRequest(IEnumerable<FileSystemItem> items, string targetDirectory)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(targetDirectory);

        foreach (var item in items)
        {
            Items.Add(item);
        }

        TargetDirectory = targetDirectory;
    }

    public Collection<FileSystemItem> Items { get; } = new();
    public string TargetDirectory { get; }
}
