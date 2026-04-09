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

    public static void Save(AppSettings settings)
    {
        var basePath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appFolder = Path.Combine(basePath, "kmrLauncher/settings.json");
        
        var json = JsonSerializer.Serialize(settings, Options);
        File.WriteAllText(appFolder, json);
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
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }
}