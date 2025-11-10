using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WhisperFTPApp.Configurations;
using WhisperFTPApp.Data;
using WhisperFTPApp.Extensions;
using WhisperFTPApp.Logger;
using WhisperFTPApp.Services;
using WhisperFTPApp.Views;

namespace WhisperFTPApp;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        try
        {
            StaticFileLogger.LogInformation("Setting initial language...");
            LocalizationService.Instance.SetLanguage("en");
            StaticFileLogger.LogInformation("Language set successfully");
        }
        catch (Exception ex)
        {
            StaticFileLogger.LogError($"Error setting language: {ex}");
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

            InitializeDatabase(_serviceProvider);

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = _serviceProvider.GetRequiredService<MainWindow>();

                desktop.Exit += OnExit;

                StaticFileLogger.LogInformation("Application initialized successfully");
            }

            base.OnFrameworkInitializationCompleted();
        }
        catch (Exception ex)
        {
            StaticFileLogger.LogError($"Application startup failed: {ex.Message}\nStackTrace: {ex.StackTrace}");
            throw;
        }
    }

    private static void InitializeDatabase(IServiceProvider serviceProvider)
    {
        try
        {
            var dbPath = PathManager.GetDatabasePath();
            StaticFileLogger.LogInformation($"Initializing database at: {dbPath}");

            var contextFactory = serviceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();

            using var context = contextFactory.CreateDbContext();

            context.Database.EnsureCreated();

            StaticFileLogger.LogInformation("Database initialized successfully");
        }
        catch (Exception dbEx)
        {
            StaticFileLogger.LogError($"Database initialization failed: {dbEx}");
            throw;
        }
    }

    private void OnExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        try
        {
            StaticFileLogger.LogInformation("Disposing services...");

            _serviceProvider?.Dispose();

            StaticFileLogger.LogInformation("Application closed successfully");
        }
        catch (Exception ex)
        {
            StaticFileLogger.LogError($"Error during shutdown: {ex}");
        }
    }
}
