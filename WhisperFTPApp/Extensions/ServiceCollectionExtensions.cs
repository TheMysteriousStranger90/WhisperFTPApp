using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WhisperFTPApp.Data;
using WhisperFTPApp.Services;
using WhisperFTPApp.Services.Interfaces;
using WhisperFTPApp.ViewModels;
using WhisperFTPApp.Views;

namespace WhisperFTPApp.Extensions;

public static class ServiceCollectionExtensions
{
    public static void AddCommonServices(this IServiceCollection collection)
    {
        collection.AddDbContext<AppDbContext>(options =>
                options.UseSqlite("Data Source=ftpclient.db"),
            ServiceLifetime.Singleton);
        
        collection.AddSingleton<ISettingsService, SettingsService>();
        collection.AddSingleton<IFtpService, FtpService>();
    }
    
    public static void AddCommonViewModels(this IServiceCollection collection)
    {
        collection.AddTransient<MainWindowViewModel>();
    }
    
    public static void AddCommonWindows(this IServiceCollection collection)
    {
        collection.AddTransient<MainWindow>();
    }
}