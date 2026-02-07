using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WhisperFTPApp.Data;
using WhisperFTPApp.Extensions;
using WhisperFTPApp.Logger;
using WhisperFTPApp.Services;
using WhisperFTPApp.Services.Interfaces;
using WhisperFTPApp.Views;

namespace WhisperFTPApp;

internal sealed partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        if (!Design.IsDesignMode)
        {
            try
            {
                StaticFileLogger.LogInformation("Application initializing...");
                LocalizationService.Instance.SetLanguage("en");
                StaticFileLogger.LogInformation("Language set successfully");
            }
            catch (Exception ex)
            {
                StaticFileLogger.LogException(ex, "Error setting language");
            }
        }
    }

    public override void OnFrameworkInitializationCompleted()
    {
        try
        {
            BindingPlugins.DataValidators.RemoveAt(0);

            var collection = new ServiceCollection();

            collection.AddCommonServices();
            collection.AddCommonViewModels();
            collection.AddCommonWindows();

            _serviceProvider = collection.BuildServiceProvider();

            if (!Design.IsDesignMode)
            {
                InitializeDatabase();
                _ = InitializeServicesAsync();
            }

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = _serviceProvider.GetRequiredService<MainWindow>();
                desktop.Exit += OnExit;

                if (!Design.IsDesignMode)
                {
                    StaticFileLogger.LogInformation("Application initialized successfully");
                }
            }

            base.OnFrameworkInitializationCompleted();
        }
        catch (Exception ex)
        {
            if (!Design.IsDesignMode)
            {
                StaticFileLogger.LogException(ex, "Application startup failed");
            }
            throw;
        }
    }

    private void InitializeDatabase()
    {
        if (_serviceProvider == null) return;

        try
        {
            var pathManager = _serviceProvider.GetRequiredService<IPathManager>();
            var dbPath = pathManager.GetDatabasePath();
            StaticFileLogger.LogInformation($"Initializing database at: {dbPath}");

            var contextFactory = _serviceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();

            using var context = contextFactory.CreateDbContext();
            context.Database.EnsureCreated();

            StaticFileLogger.LogInformation("Database initialized successfully");
        }
        catch (Exception dbEx)
        {
            StaticFileLogger.LogException(dbEx, "Database initialization failed");
            throw;
        }
    }

    private async Task InitializeServicesAsync()
    {
        if (_serviceProvider == null) return;

        try
        {
            var backgroundService = _serviceProvider.GetRequiredService<IBackgroundService>();
            await backgroundService.InitializeAsync().ConfigureAwait(false);
            StaticFileLogger.LogInformation("Background service initialized");
        }
        catch (Exception ex)
        {
            StaticFileLogger.LogException(ex, "Service initialization failed");
        }
    }

    private void OnExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        try
        {
            StaticFileLogger.LogInformation("Application shutting down...");
            StaticFileLogger.Flush();

            _serviceProvider?.Dispose();

            StaticFileLogger.LogInformation("Application closed successfully");
            StaticFileLogger.Flush();
        }
        catch (Exception ex)
        {
            StaticFileLogger.LogException(ex, "Error during shutdown");
        }
    }
}
