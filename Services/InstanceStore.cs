using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using KMRLauncherMvvm.Models;

namespace KMRLauncherMvvm.Services;

public class InstanceInfoFile
{
    public int Format { get; set; } = InstanceStore.Format;
    public string Name { get; set; } = "";
    public string Version { get; set; } = "";
    public List<string> CompatibleKspVersions { get; set; } = [];
    public string LaunchArguments { get; set; } = "";
    public string PrefixPath { get; set; } = "";
    public int PlayTime { get; set; }
    public DateTime LastPlayed { get; set; }

    public static InstanceInfoFile From(Instance instance) => new()
    {
        Name = instance.Name,
        Version = instance.Version,
        CompatibleKspVersions = instance.CompatibleKspVersions,
        LaunchArguments = instance.LaunchArguments,
        PrefixPath = instance.PrefixPath,
        PlayTime = instance.PlayTime,
        LastPlayed = instance.LastPlayed
    };
}

public class InstanceModsFile
{
    public int Format { get; set; } = InstanceStore.Format;
    public List<ModVersion> Mods { get; set; } = [];
    public Dictionary<string, InstalledModRecord> ModRecords { get; set; } = [];

    public static InstanceModsFile From(Instance instance) => new()
    {
        Mods = instance.Mods,
        ModRecords = instance.ModRecords
    };
}

public static class InstanceStore
{
    public const string FolderName = ".kmr";
    public const string InfoFileName = "instance.json";
    public const string ModsFileName = "mods.json";
    public const int Format = 1;

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true
    };

    public static string DataFolder(string root) => Path.Combine(root, FolderName);
    public static string StagingFolder(string root) => Path.Combine(root, FolderName, "staging");
    private static string InfoPath(string root) => Path.Combine(root, FolderName, InfoFileName);
    private static string ModsPath(string root) => Path.Combine(root, FolderName, ModsFileName);

    public static Instance Load(string root)
    {
        if (!Directory.Exists(root)) return Missing(root, "The folder isn't there anymore.");
        if (!File.Exists(InfoPath(root)))
            return Missing(root, $"The folder has no {FolderName}/{InfoFileName}.");
        if (!File.Exists(ModsPath(root)))
            return Missing(root, $"The folder has no {FolderName}/{ModsFileName}.");

        try
        {
            var info = JsonSerializer.Deserialize<InstanceInfoFile>(File.ReadAllText(InfoPath(root)), Options)
                       ?? throw new JsonException($"{InfoFileName} is empty.");
            var mods = JsonSerializer.Deserialize<InstanceModsFile>(File.ReadAllText(ModsPath(root)), Options)
                       ?? throw new JsonException($"{ModsFileName} is empty.");
            if (info.Format > Format || mods.Format > Format)
                return Missing(root, "The data was written by a newer version of the launcher.");

            return new Instance(info.Name.Trim().Length > 0 ? info.Name : FolderTitle(root), root, info.Version ?? "")
            {
                CompatibleKspVersions = info.CompatibleKspVersions ?? [],
                LaunchArguments = info.LaunchArguments ?? "",
                PrefixPath = info.PrefixPath ?? "",
                PlayTime = info.PlayTime,
                LastPlayed = info.LastPlayed,
                Mods = mods.Mods ?? [],
                ModRecords = mods.ModRecords ?? []
            };
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException
                                      or NotSupportedException)
        {
            return Missing(root, $"The launcher data in {FolderName} can't be read: {e.Message}");
        }
    }

    public static void SaveInfo(Instance instance)
    {
        Directory.CreateDirectory(DataFolder(instance.RootPath));
        AtomicFile.WriteAllText(InfoPath(instance.RootPath), InfoJson(instance));
    }

    public static void SaveMods(Instance instance)
    {
        Directory.CreateDirectory(DataFolder(instance.RootPath));
        AtomicFile.WriteAllText(ModsPath(instance.RootPath), ModsJson(instance));
    }

    public static void Save(Instance instance)
    {
        SaveInfo(instance);
        SaveMods(instance);
    }

    public static bool SameData(Instance a, Instance b) =>
        InfoJson(a) == InfoJson(b) && ModsJson(a) == ModsJson(b);

    public static (Instance? Found, string? Error) Relocate(AppSettings settings, Instance missing, string folder)
    {
        var index = settings.Instances.IndexOf(missing);
        if (index < 0) return (null, "The instance isn't in the list anymore.");

        if (settings.Instances.Any(i => i != missing && WinePrefixes.SamePath(i.RootPath, folder)))
            return (null, "That folder is already in the list.");

        var found = Load(Path.GetFullPath(folder).TrimEnd('/', '\\'));
        if (found.IsMissing) return (null, found.LoadError);

        found.IsPrimary = missing.IsPrimary;
        settings.Instances[index] = found;
        return (found, null);
    }

    private static string InfoJson(Instance instance) =>
        JsonSerializer.Serialize(InstanceInfoFile.From(instance), Options);

    private static string ModsJson(Instance instance) =>
        JsonSerializer.Serialize(InstanceModsFile.From(instance), Options);

    private static Instance Missing(string root, string reason) =>
        new(FolderTitle(root), root, "") { LoadError = reason };

    private static string FolderTitle(string root)
    {
        var name = Path.GetFileName(root.TrimEnd('/', '\\'));
        return name.Length > 0 ? name : root;
    }
}
