using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WhisperFTPApp.Data;
using WhisperFTPApp.Services;
using WhisperFTPApp.Services.Interfaces;
using WhisperFTPApp.ViewModels;
using WhisperFTPApp.Views;

namespace WhisperFTPApp.Extensions;

internal static class ServiceCollectionExtensions
{
    public static void AddCommonServices(this IServiceCollection collection)
    {
        collection.AddSingleton<IPathManager, PathManagerService>();

        collection.AddDbContextFactory<AppDbContext>((serviceProvider, options) =>
        {
            var pathManager = serviceProvider.GetRequiredService<IPathManager>();
            var dbPath = pathManager.GetDatabasePath();

            options.UseSqlite($"Data Source={dbPath}",
                sqliteOptions =>
                {
                    sqliteOptions.CommandTimeout(30);
                    sqliteOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
                });
#if DEBUG
            options.EnableSensitiveDataLogging();
            options.EnableDetailedErrors();
#endif
        });

        collection.AddSingleton<ICredentialEncryption, CredentialEncryptionService>();
        collection.AddSingleton<ISettingsService, SettingsService>();
        collection.AddSingleton<IFtpService, FtpService>();
        collection.AddSingleton<IBackgroundService, BackgroundService>();
        collection.AddSingleton<IWifiScannerService, WifiScannerService>();
    }

    public static void AddCommonViewModels(this IServiceCollection collection)
    {
        collection.AddTransient<MainWindowViewModel>();
        collection.AddTransient<SettingsWindowViewModel>();
        collection.AddTransient<ScanWindowViewModel>();
    }

    public static void AddCommonWindows(this IServiceCollection collection)
    {
        collection.AddTransient<MainWindow>();
        collection.AddTransient<MainView>();
        collection.AddTransient<SettingsView>();
        collection.AddTransient<ScanView>();
    }
}
