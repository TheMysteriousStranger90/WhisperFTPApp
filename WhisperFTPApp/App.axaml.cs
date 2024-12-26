using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using WhisperFTPApp.Data;
using WhisperFTPApp.Extensions;
using WhisperFTPApp.Logger;
using WhisperFTPApp.Services;
using WhisperFTPApp.ViewModels;
using WhisperFTPApp.Views;

namespace WhisperFTPApp;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        
        var localization = LocalizationService.Instance;
        try 
        {
            StaticFileLogger.LogInformation("Setting initial language...");
            localization.SetLanguage("en");
            StaticFileLogger.LogInformation("Language set successfully");
        }
        catch (Exception ex)
        {
            StaticFileLogger.LogError($"Error setting language: {ex}");
        }
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var collection = new ServiceCollection();
        collection.AddCommonServices();
        collection.AddCommonViewModels();
        collection.AddCommonWindows();

        var serviceProvider = collection.BuildServiceProvider();
        var mainWindow = serviceProvider.GetRequiredService<MainWindow>();
        
        using (var scope = serviceProvider.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            context.Database.EnsureCreated();
        }

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = mainWindow;
        }

        base.OnFrameworkInitializationCompleted();
    }
}