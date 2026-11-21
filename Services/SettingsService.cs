using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using KMRLauncherMvvm.Models;

namespace KMRLauncherMvvm.Services;

public static class SettingsService
{
    public static readonly string DefaultFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "kmrLauncher", "settings.json");

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true
    };

    public static void Write(AppSettings settings, string file, Instance? changed = null, bool modsChanged = false)
    {
        if (changed is { IsMissing: false })
        {
            if (string.IsNullOrWhiteSpace(changed.PrefixPath))
                changed.PrefixPath = WinePrefixes.DefaultFor(changed, settings.Instances);
            InstanceStore.SaveInfo(changed);
            if (modsChanged) InstanceStore.SaveMods(changed);
        }

        settings.InstanceFolders = [.. settings.Instances.Select(i => i.RootPath)];
        settings.PrimaryInstance = settings.Instances.FirstOrDefault(i => i.IsPrimary)?.RootPath;
        AtomicFile.WriteAllText(file, JsonSerializer.Serialize(settings, Options));
    }

    public static AppSettings Load() => Load(DefaultFile);

    public static AppSettings Load(string file)
    {
        if (!File.Exists(file))
            return new AppSettings();

        try
        {
            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(file), Options) ?? new AppSettings();
            LoadInstances(settings);
            return settings;
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException)
        {
            var copy = file + ".broken";
            try
            {
                File.Copy(file, copy, true);
            }
            catch (Exception copyError) when (copyError is IOException or UnauthorizedAccessException)
            {
            }

            return new AppSettings
            {
                LoadProblem = $"settings.json couldn't be read ({e.Message}). The launcher started with empty " +
                              $"settings; the old file was copied to {Path.GetFileName(copy)}."
            };
        }
    }

    private static void LoadInstances(AppSettings settings)
    {
        settings.Instances = new ObservableCollection<Instance>(settings.InstanceFolders
            .Where(f => !string.IsNullOrWhiteSpace(f))
            .Distinct()
            .Select(InstanceStore.Load));

        foreach (var instance in settings.Instances)
            instance.IsPrimary = settings.PrimaryInstance is { } primary && instance.RootPath == primary;

        WinePrefixes.AssignMissing([.. settings.UsableInstances]);
    }
}
