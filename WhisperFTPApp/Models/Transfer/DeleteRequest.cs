using System.Collections.ObjectModel;

namespace WhisperFTPApp.Models.Transfer;

public class DeleteRequest
{
    public DeleteRequest(IEnumerable<FileSystemItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        foreach (var item in items)
        {
            Items.Add(item);
        }
    }

    public Collection<FileSystemItem> Items { get; } = new();
}
