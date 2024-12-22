using System;
using System.Windows.Input;
using ReactiveUI;

namespace WhisperFTPApp.Models.Navigations;

public class BreadcrumbItem : ReactiveObject
{
    public string Path { get; }
    public string Display { get; }
    public ICommand NavigateCommand { get; }

    public BreadcrumbItem(string path, string display, Action<string> navigationAction)
    {
        Path = path;
        Display = display;
        NavigateCommand = ReactiveCommand.Create(() => navigationAction(Path));
    }
}