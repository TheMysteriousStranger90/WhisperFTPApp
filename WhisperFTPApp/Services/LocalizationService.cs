using System;
using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.Resources;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using WhisperFTPApp.Assets;

namespace WhisperFTPApp.Services;

public class LocalizationService
{
    private static LocalizationService _instance;
    public static LocalizationService Instance => _instance ??= new LocalizationService();

    private readonly ResourceManager _resourceManager;
    private const string DefaultLanguage = "en";
    private const string RussianLanguage = "ru-RU";

    public event EventHandler<CultureInfo> CultureChanged;
    public CultureInfo CurrentCulture { get; private set; }

    private LocalizationService()
    {
        try
        {
            _resourceManager = Resources.ResourceManager;
            CurrentCulture = new CultureInfo(DefaultLanguage);
            SetLanguage(DefaultLanguage);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"LocalizationService initialization error: {ex}");
        }
    }

    public string GetString(string key)
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
    }

    public void SetLanguage(string cultureName)
    {
        try
        {
            Debug.WriteLine($"Changing language to: {cultureName}");
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
                    newDict[entry.Key.ToString()] = entry.Value.ToString();
                    Debug.WriteLine($"Added resource: {entry.Key} = {entry.Value}");
                }
            }

            app.Resources.MergedDictionaries.Clear();
            app.Resources.MergedDictionaries.Add(newDict);
            CultureChanged?.Invoke(this, CurrentCulture);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error changing language: {ex}");
        }
    }
}