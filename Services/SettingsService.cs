using System;
using System.IO;
using System.Text.Json;
using KMRLauncherMvvm.Models;

namespace KMRLauncherMvvm.Services;

public static class SettingsService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true
    };

    public static event EventHandler<AppSettings>? SettingsChanged;
    
    public static void Save(AppSettings settings)
    {
        var basePath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appFolder = Path.Combine(basePath, "kmrLauncher/settings.json");
        
        WinePrefixes.AssignMissing(settings.Instances);
        var json = JsonSerializer.Serialize(settings, Options);
        File.WriteAllText(appFolder, json);
        
        App.Settings = settings;
        
        SettingsChanged?.Invoke(null, settings);
    }

    public static AppSettings Load()
    {
        var basePath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appFolder = Path.Combine(basePath, "kmrLauncher/settings.json");
        
        if (!File.Exists(appFolder))
            return new AppSettings();

        try
        {
            var json = File.ReadAllText(appFolder);
            var settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();

            // Instances saved before Phase 5 task 6 get their prefix path here; it's written on the next save
            WinePrefixes.AssignMissing(settings.Instances);
            return settings;
        }
        catch
        {
            return new AppSettings();
        }
    }
    
    public static AppSettings Reload()
    {
        var settings = Load();
        App.Settings = settings;
        SettingsChanged?.Invoke(null, settings);
        return settings;
    }
}