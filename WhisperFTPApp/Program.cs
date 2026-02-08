using Avalonia;
using Avalonia.ReactiveUI;
using WhisperFTPApp.Services;

namespace WhisperFTPApp;

internal static class Program
{
    private static SingleInstanceService? _singleInstance;

    [STAThread]
    public static int Main(string[] args)
    {
        _singleInstance = new SingleInstanceService();

        if (!_singleInstance.TryAcquire())
        {
            SingleInstanceService.BringExistingInstanceToFront();
            _singleInstance.Dispose();
            return 1;
        }

        try
        {
            return BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            _singleInstance.Dispose();
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace()
            .UseReactiveUI();
}
