using System;
using System.Windows.Input;

namespace WhisperFTPApp.Commands;

public class BreadcrumbNavigationCommand : ICommand
{
    private readonly Action<string> _execute;
    
    public BreadcrumbNavigationCommand(Action<string> execute)
    {
        _execute = execute;
    }

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter)
    {
        if (parameter is string path)
        {
            _execute(path);
        }
    }

    public event EventHandler? CanExecuteChanged;
}