using System.Diagnostics;
using System.IO;
using System.Reactive;
using ReactiveUI;
using WhisperFTPApp.Logger;
using WhisperFTPApp.Services.Interfaces;
using WhisperFTPApp.Settings;

namespace WhisperFTPApp.ViewModels;

public class SettingsWindowViewModel : ViewModelBase
{
    private string LogFilePath => StaticFileLogger.GetLogFolderPath();
    public ReactiveCommand<Unit, Unit> OpenLogFolderCommand { get; }
    private readonly ISettingsService _settingsService;
    private readonly IBackgroundService _backgroundService;
    private string _selectedBackground;

    public BackgroundSettings BackgroundSettings { get; } = new();

    public string SelectedBackground
    {
        get => _selectedBackground;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedBackground, value);
            _ = _backgroundService.ChangeBackgroundAsync(value);
        }
    }

    public SettingsWindowViewModel(ISettingsService settingsService, IBackgroundService backgroundService)
    {
        _settingsService = settingsService;
        _backgroundService = backgroundService;
        _selectedBackground = _backgroundService.CurrentBackground;

        OpenLogFolderCommand = ReactiveCommand.Create(() =>
        {
            var path = Path.GetDirectoryName(LogFilePath);
            if (path != null)
            {
                Process.Start("explorer.exe", path);
                StaticFileLogger.LogInformation("Log folder opened by user");
            }
        });
    }
}