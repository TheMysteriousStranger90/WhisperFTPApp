using System.Collections.ObjectModel;
using ReactiveUI;

namespace WhisperFTPApp.Models;

public sealed class FileSystemItem : ReactiveObject
{
    private string _name = string.Empty;
    private string _fullPath = string.Empty;
    private bool _isDirectory;
    private long _size;
    private DateTime _modified;
    private string _type = string.Empty;
    private FileSystemItem? _parent;
    private bool _isSelected;

    public string Name
    {
        get => _name;
        set => this.RaiseAndSetIfChanged(ref _name, value);
    }

    public string FullPath
    {
        get => _fullPath;
        set => this.RaiseAndSetIfChanged(ref _fullPath, value);
    }

    public bool IsDirectory
    {
        get => _isDirectory;
        set => this.RaiseAndSetIfChanged(ref _isDirectory, value);
    }

    public long Size
    {
        get => _size;
        set => this.RaiseAndSetIfChanged(ref _size, value);
    }

    public DateTime Modified
    {
        get => _modified;
        set => this.RaiseAndSetIfChanged(ref _modified, value);
    }

    public string Type
    {
        get => _type;
        set => this.RaiseAndSetIfChanged(ref _type, value);
    }

    public FileSystemItem? Parent
    {
        get => _parent;
        set => this.RaiseAndSetIfChanged(ref _parent, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => this.RaiseAndSetIfChanged(ref _isSelected, value);
    }

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
