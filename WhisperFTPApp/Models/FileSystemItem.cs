using System.Collections.ObjectModel;

namespace WhisperFTPApp.Models;

public sealed class FileSystemItem
{
    public string Name { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public bool IsDirectory { get; set; }
    public long Size { get; set; }
    public DateTime Modified { get; set; }
    public string Type { get; set; } = string.Empty;
    public FileSystemItem? Parent { get; set; }
    public ObservableCollection<FileSystemItem> Children { get; } = new();

    public string GetRelativePath(string basePath)
    {
        ArgumentNullException.ThrowIfNull(basePath);
        return FullPath.Replace(basePath, string.Empty, StringComparison.Ordinal).TrimStart('\\', '/');
    }

    public string? ParentPath => Parent?.FullPath ?? Path.GetDirectoryName(FullPath);

    public void AddChild(FileSystemItem child)
    {
        ArgumentNullException.ThrowIfNull(child);
        child.Parent = this;
        Children.Add(child);
    }

    public void RemoveChild(FileSystemItem child)
    {
        ArgumentNullException.ThrowIfNull(child);
        child.Parent = null;
        Children.Remove(child);
    }

    public FileSystemItem Root
    {
        get
        {
            var current = this;
            while (current.Parent != null)
            {
                current = current.Parent;
            }

            return current;
        }
    }

    public IReadOnlyList<FileSystemItem> GetPathToRoot()
    {
        var path = new List<FileSystemItem>();
        var current = (FileSystemItem?)this;
        while (current != null)
        {
            path.Add(current);
            current = current.Parent;
        }

        path.Reverse();
        return path.AsReadOnly();
    }
}
