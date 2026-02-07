using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Reactive;
using ReactiveUI;
using WhisperFTPApp.Logger;
using WhisperFTPApp.Services;
using WhisperFTPApp.Services.Interfaces;
using WhisperFTPApp.Settings;

namespace WhisperFTPApp.ViewModels;

internal sealed class SettingsWindowViewModel : ReactiveObject
{
    private readonly LocalizationService _localizationService;
    private readonly IBackgroundService _backgroundService;
    private readonly ISettingsService _settingsService;
    private CultureInfo _selectedLanguage;
    private string _selectedBackground;

    public ObservableCollection<CultureInfo> AvailableLanguages { get; }

    public CultureInfo SelectedLanguage
    {
        get => _selectedLanguage;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedLanguage, value);
            if (value != null)
            {
                _localizationService.SetLanguage(value.Name);
                _ = SaveLanguageAsync(value.Name);
            }
        }
    }

    public static string LogFilePath => StaticFileLogger.LogFolderPath;

    public ReactiveCommand<Unit, Unit> OpenLogFolderCommand { get; }

    public BackgroundSettings BackgroundSettings { get; } = new();

    public string SelectedBackground
    {
        get => _selectedBackground;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedBackground, value);
            _ = ChangeBackgroundAsync(value);
        }
    }

    public SettingsWindowViewModel(
        ISettingsService settingsService,
        IBackgroundService backgroundService,
        LocalizationService localizationService)
    {
        ArgumentNullException.ThrowIfNull(settingsService);
        ArgumentNullException.ThrowIfNull(backgroundService);
        ArgumentNullException.ThrowIfNull(localizationService);

        _settingsService = settingsService;
        _backgroundService = backgroundService;
        _selectedBackground = _backgroundService.CurrentBackground;

        OpenLogFolderCommand = ReactiveCommand.Create(() =>
        {
            var path = LogFilePath;
            if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true,
                    Verb = "open"
                });
                StaticFileLogger.LogInformation("Log folder opened by user");
            }
        });

        _localizationService = localizationService;
        AvailableLanguages =
        [
            new CultureInfo("en"),
            new CultureInfo("ru-RU")
        ];
        _selectedLanguage = AvailableLanguages.FirstOrDefault(l =>
            l.Name == _localizationService.CurrentCulture.Name) ?? AvailableLanguages[0];
    }

    private async Task ChangeBackgroundAsync(string path)
    {
        try
        {
            await _backgroundService.ChangeBackgroundAsync(path).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            StaticFileLogger.LogError($"Error changing background: {ex.Message}");
        }
    }

    private async Task SaveLanguageAsync(string language)
    {
        try
        {
            await _settingsService.SaveLanguageSettingAsync(language).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            StaticFileLogger.LogError($"Error saving language: {ex.Message}");
        }
    }
}
