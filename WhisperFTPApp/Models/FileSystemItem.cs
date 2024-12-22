using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;

namespace WhisperFTPApp.Models;

public class FileSystemItem
{
    public string Name { get; set; }
    public string FullPath { get; set; }
    public bool IsDirectory { get; set; }
    public long Size { get; set; }
    public DateTime Modified { get; set; }
    public string Type { get; set; }
    public FileSystemItem Parent { get; set; }
    public ObservableCollection<FileSystemItem> Children { get; set; } = new();

    public string GetRelativePath(string basePath)
    {
        return FullPath.Replace(basePath, string.Empty).TrimStart('\\', '/');
    }

    public string GetParentPath()
    {
        return Parent?.FullPath ?? Path.GetDirectoryName(FullPath);
    }

    public void AddChild(FileSystemItem child)
    {
        child.Parent = this;
        Children.Add(child);
    }

    public void RemoveChild(FileSystemItem child)
    {
        child.Parent = null;
        Children.Remove(child);
    }

    public FileSystemItem GetRoot()
    {
        var current = this;
        while (current.Parent != null)
        {
            current = current.Parent;
        }
        return current;
    }

    public List<FileSystemItem> GetPathToRoot()
    {
        var path = new List<FileSystemItem>();
        var current = this;
        while (current != null)
        {
            path.Add(current);
            current = current.Parent;
        }
        path.Reverse();
        return path;
    }
}