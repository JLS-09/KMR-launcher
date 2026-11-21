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
        if (settings.UsesLegacyFile)
        {
            WinePrefixes.AssignMissing(settings.Instances);
            AtomicFile.WriteAllText(file, JsonSerializer.Serialize(LegacySettingsFile.From(settings), Options));
            return;
        }

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
            var json = File.ReadAllText(file);
            using var document = JsonDocument.Parse(json);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, Options) ?? new AppSettings();

            if (document.RootElement.ValueKind == JsonValueKind.Object &&
                document.RootElement.TryGetProperty(nameof(LegacySettingsFile.Instances), out var old) &&
                old.ValueKind == JsonValueKind.Array)
                return Migrate(file, json, settings);

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

    private static AppSettings Migrate(string file, string json, AppSettings settings)
    {
        var instances = JsonSerializer.Deserialize<LegacySettingsFile>(json, Options)?.Instances ?? [];
        WinePrefixes.AssignMissing(instances);

        try
        {
            foreach (var instance in instances.Where(i => Directory.Exists(i.RootPath)))
            {
                InstanceStore.Save(instance);
                var readBack = InstanceStore.Load(instance.RootPath);
                if (readBack.LoadError is { } error)
                    throw new IOException($"{instance.Name}: {error}");
                if (!InstanceStore.SameData(instance, readBack))
                    throw new IOException(
                        $"{instance.Name}: {InstanceStore.FolderName} doesn't read back the same as settings.json.");
            }

            File.Copy(file, file + ".bak", true);

            settings.Instances = new ObservableCollection<Instance>(instances.Select(instance =>
            {
                if (Directory.Exists(instance.RootPath)) return instance;

                var missing = InstanceStore.Load(instance.RootPath);
                missing.Name = instance.Name;
                missing.IsPrimary = instance.IsPrimary;
                return missing;
            }));
            Write(settings, file);
            return settings;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            settings.Instances = new ObservableCollection<Instance>(instances);
            settings.UsesLegacyFile = true;
            settings.LoadProblem = "Couldn't move the instance data into the instance folders " +
                                   $"({e.Message}). The launcher keeps using settings.json for now and tries " +
                                   "again at the next start.";
            return settings;
        }
    }
}
