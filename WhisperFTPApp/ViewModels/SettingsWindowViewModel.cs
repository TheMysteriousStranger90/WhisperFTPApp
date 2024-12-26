using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Threading;
using ReactiveUI;
using WhisperFTPApp.Logger;
using WhisperFTPApp.Services;
using WhisperFTPApp.Services.Interfaces;
using WhisperFTPApp.Settings;

namespace WhisperFTPApp.ViewModels;

public class SettingsWindowViewModel : ViewModelBase
{
    private readonly LocalizationService _localizationService;
    public ObservableCollection<CultureInfo> AvailableLanguages { get; }
    private CultureInfo _selectedLanguage;

    public CultureInfo SelectedLanguage
    {
        get => _selectedLanguage;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedLanguage, value);
            if (value != null)
            {
                _localizationService.SetLanguage(value.Name);
            }
        }
    }
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

    public SettingsWindowViewModel(ISettingsService settingsService, IBackgroundService backgroundService, LocalizationService localizationService)
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
        
        _localizationService = localizationService;
        AvailableLanguages = new ObservableCollection<CultureInfo>
        {
            new("en"),
            new("ru-RU")
        };
        _selectedLanguage = AvailableLanguages.First(l => 
            l.Name == Thread.CurrentThread.CurrentUICulture.Name);
    }
}