using System.Windows.Input;

namespace WhisperFTPApp.Commands;

internal sealed class BreadcrumbNavigationCommand : ICommand
{
    private readonly Action<string> _execute;

    public BreadcrumbNavigationCommand(Action<string> execute)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
    }

    public bool CanExecute(object? parameter)
    {
        return parameter is string;
    }

    public void Execute(object? parameter)
    {
        if (parameter is string path)
        {
            _execute(path);
        }
    }

    public event EventHandler? CanExecuteChanged;

    public void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
