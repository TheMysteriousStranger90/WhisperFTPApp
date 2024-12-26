using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Microsoft.EntityFrameworkCore;
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

            // Configure SQLite with specific options
            collection.AddDbContext<AppDbContext>(
                options =>
                {
                    options.UseSqlite("Data Source=DatabaseWhisperFTPApp.db",
                        sqliteOptions => { sqliteOptions.CommandTimeout(30); });
                }, ServiceLifetime.Singleton);

            collection.AddCommonServices();
            collection.AddCommonViewModels();
            collection.AddCommonWindows();

            var serviceProvider = collection.BuildServiceProvider();

            using (var scope = serviceProvider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                try
                {
                    StaticFileLogger.LogInformation("Initializing database...");
                    context.Database.EnsureCreated();

                    // Force connection close
                    context.Database.GetDbConnection().Close();

                    StaticFileLogger.LogInformation("Database initialized successfully");
                }
                catch (Exception dbEx)
                {
                    StaticFileLogger.LogError($"Database initialization failed: {dbEx}");
                    throw;
                }
            }

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = serviceProvider.GetRequiredService<MainWindow>();
                desktop.ShutdownRequested += (s, e) =>
                {
                    // Cleanup on shutdown
                    using var scope = serviceProvider.CreateScope();
                    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    context.Database.GetDbConnection().Close();
                };

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
}