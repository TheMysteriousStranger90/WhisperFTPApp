using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Resources;
using Avalonia;
using Avalonia.Controls;
using WhisperFTPApp.Assets;
using WhisperFTPApp.Constants;
using WhisperFTPApp.Events;

namespace WhisperFTPApp.Services;

public class LocalizationService
{
    private static volatile LocalizationService? _instance;
    private static readonly object _instanceLock = new();

    public static LocalizationService Instance
    {
        get
        {
            var instance = _instance;
            if (instance != null)
                return instance;

            lock (_instanceLock)
            {
                return _instance ??= new LocalizationService();
            }
        }
    }

    private readonly ResourceManager _resourceManager = Resources.ResourceManager;
    private readonly ConcurrentDictionary<string, string> _cache = new();

    public event EventHandler<CultureChangedEventArgs>? CultureChanged;
    public CultureInfo CurrentCulture { get; private set; }

    private LocalizationService()
    {
        try
        {
            CurrentCulture = new CultureInfo(AppConstants.DefaultLanguage);
            SetLanguage(AppConstants.DefaultLanguage);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"LocalizationService initialization error: {ex}");
            CurrentCulture = CultureInfo.InvariantCulture;
        }
    }

    public string GetString(string key)
    {
        var cacheKey = $"{CurrentCulture.Name}_{key}";

        return _cache.GetOrAdd(cacheKey, _ =>
        {
            try
            {
                var value = _resourceManager.GetString(key, CurrentCulture);
                Debug.WriteLine($"GetString: {key} = {value}");
                return value ?? key;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting string for key {key}: {ex}");
                return key;
            }
        });
    }

    public void SetLanguage(string cultureName)
    {
        try
        {
            Debug.WriteLine($"Changing language to: {cultureName}");

            _cache.Clear();

            CurrentCulture = new CultureInfo(cultureName);
            Thread.CurrentThread.CurrentUICulture = CurrentCulture;
            Thread.CurrentThread.CurrentCulture = CurrentCulture;

            var app = Application.Current;
            if (app?.Resources == null) return;

            var newDict = new ResourceDictionary();
            var resourceSet = _resourceManager.GetResourceSet(CurrentCulture, true, true);

            if (resourceSet != null)
            {
                foreach (DictionaryEntry entry in resourceSet)
                {
                    if (entry.Key != null && entry.Value != null)
                    {
                        var keyStr = entry.Key.ToString() ?? throw new InvalidOperationException();
                        newDict[keyStr] = entry.Value.ToString();
                        Debug.WriteLine($"Added resource: {entry.Key} = {entry.Value}");
                    }
                }
            }

            app.Resources.MergedDictionaries.Clear();
            app.Resources.MergedDictionaries.Add(newDict);
            CultureChanged?.Invoke(this, new CultureChangedEventArgs(CurrentCulture));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error changing language: {ex}");
        }
    }
}
