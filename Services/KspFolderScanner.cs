using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using KMRLauncherMvvm.Models;

namespace KMRLauncherMvvm.Services;

public record ImportedMod(ModVersion Version, InstalledModRecord Record);

public class KspFolderScan
{
    public required string Folder { get; init; }
    public string? Version { get; init; }
    public string? BuildId { get; init; }
    public string? Error { get; init; }
    public Instance? KmrData { get; init; }
    public bool HasRegistry { get; init; }
    public List<ImportedMod> Mods { get; init; } = [];
    public List<string> Unreadable { get; init; } = [];
    public int UnreadableMods => Unreadable.Count;
    public List<string>? CkanCompatible { get; init; }
    public List<string> UnownedFolders { get; init; } = [];

    public bool CanAdd => Error is null && Version is not null;
    public int Picked => Mods.Count(m => m.Record.IsUserRequested);
    public int Dependencies => Mods.Count - Picked;
}

public static class KspFolderScanner
{
    private const string RegistryFile = "registry.json";
    private const string CompatibleFile = "compatible_ksp_versions.json";

    public static KspFolderScan Scan(string folder, IEnumerable<Instance> instances, IEnumerable<Mod>? known = null)
    {
        folder = Path.GetFullPath(WinePrefixes.Expand(folder.Trim())).TrimEnd('/', '\\');
        if (!Directory.Exists(folder))
            return new KspFolderScan { Folder = folder, Error = "That folder doesn't exist." };
        if (instances.Any(i => WinePrefixes.SamePath(i.RootPath, folder)))
            return new KspFolderScan { Folder = folder, Error = "This folder is already an instance." };

        var buildFile = new[] { "buildID64.txt", "buildID.txt" }.Select(f => Path.Combine(folder, f))
            .FirstOrDefault(File.Exists);
        if (buildFile is null)
            return new KspFolderScan
            {
                Folder = folder,
                Error = "There's no buildID64.txt here. Pick the folder that has KSP_x64.exe in it."
            };

        using var reader = new StreamReader(buildFile);
        var buildId = ZipService.ReadBuildId(reader);
        var version = KspVersions.GetKspVersionFromBuildId(buildId);
        if (version is null)
            return new KspFolderScan
            {
                Folder = folder, BuildId = buildId,
                Error = $"Build {buildId} isn't a KSP version the launcher knows."
            };

        Instance? kmrData = null;
        if (File.Exists(Path.Combine(InstanceStore.DataFolder(folder), InstanceStore.InfoFileName)))
        {
            kmrData = InstanceStore.Load(folder);
            if (kmrData.IsMissing)
                return new KspFolderScan { Folder = folder, Version = version, BuildId = buildId, Error = kmrData.LoadError };
        }

        var ckanFolder = Path.Combine(folder, "CKAN");
        var (mods, unreadable, hasRegistry) = ReadRegistry(Path.Combine(ckanFolder, RegistryFile), known);
        var owned = mods.SelectMany(m => m.Record.Files).Select(TopGameDataFolder).OfType<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (kmrData is not null)
            owned.UnionWith(kmrData.ModRecords.Values.SelectMany(r => r.Files).Select(TopGameDataFolder)
                .OfType<string>());

        var gameData = Path.Combine(folder, "GameData");
        List<string> unowned = Directory.Exists(gameData)
            ? [.. Directory.GetDirectories(gameData).Select(Path.GetFileName).OfType<string>()
                .Where(name => !owned.Contains(name))
                .OrderBy(name => name.StartsWith("Squad", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(name => name, StringComparer.OrdinalIgnoreCase)]
            : [];

        return new KspFolderScan
        {
            Folder = folder,
            Version = version,
            BuildId = buildId,
            KmrData = kmrData,
            HasRegistry = hasRegistry,
            Mods = mods,
            Unreadable = unreadable,
            CkanCompatible = ReadCompatible(Path.Combine(ckanFolder, CompatibleFile), version),
            UnownedFolders = unowned
        };
    }

    public static (List<ImportedMod> Mods, List<string> Unreadable, bool Found) ReadRegistry(string path,
        IEnumerable<Mod>? known)
    {
        if (!File.Exists(path)) return ([], [], false);

        var byId = (known ?? []).SelectMany(m => m.Versions)
            .GroupBy(v => (v.Identifier, v.Version))
            .ToDictionary(g => g.Key, g => g.First());
        List<ImportedMod> mods = [];
        List<string> unreadable = [];
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (!doc.RootElement.TryGetProperty("installed_modules", out var installed) ||
                installed.ValueKind != JsonValueKind.Object)
                return ([], [], true);

            foreach (var entry in installed.EnumerateObject())
            {
                if (!entry.Value.TryGetProperty("source_module", out var source) ||
                    source.ValueKind != JsonValueKind.Object)
                {
                    unreadable.Add(entry.Name);
                    continue;
                }

                if (source.TryGetProperty("kind", out var kind) && kind.ValueKind == JsonValueKind.String &&
                    kind.GetString() == "dlc")
                    continue;

                var parsed = JsonHelper.ParseModule(source);
                if (parsed is null)
                {
                    unreadable.Add(entry.Name);
                    continue;
                }

                var version = byId.GetValueOrDefault((parsed.Identifier, parsed.Version)) ?? parsed;
                var auto = entry.Value.TryGetProperty("auto_installed", out var a) && a.ValueKind == JsonValueKind.True;
                List<string> files = entry.Value.TryGetProperty("installed_files", out var f) &&
                                     f.ValueKind == JsonValueKind.Object
                    ? [.. f.EnumerateObject().Select(p => p.Name.Replace('\\', '/')).Where(p => !p.EndsWith('/'))]
                    : [];

                mods.Add(new ImportedMod(version, new InstalledModRecord { Files = files, IsUserRequested = !auto }));
            }
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException)
        {
            return ([], [], true);
        }

        return (mods, unreadable, true);
    }

    public static List<string>? ReadCompatible(string path, string instanceVersion)
    {
        if (!File.Exists(path)) return null;

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return null;

            var list = doc.RootElement.TryGetProperty("Versions", out var v) ? v
                : doc.RootElement.TryGetProperty("CompatibleKspVersions", out var old) ? old
                : default;
            if (list.ValueKind != JsonValueKind.Array) return null;

            var own = KspVersions.ToFullVersion(instanceVersion);
            return
            [
                .. list.EnumerateArray()
                    .Where(e => e.ValueKind == JsonValueKind.String)
                    .Select(e => ToLauncherVersion(e.GetString()!))
                    .OfType<string>()
                    .Where(e => e != own)
                    .Distinct()
            ];
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string? ToLauncherVersion(string version)
    {
        var parts = version.Trim().Split('.');
        if (parts.Length < 2 || parts.Any(p => !int.TryParse(p, out _))) return null;
        return parts.Length == 2 ? $"{parts[0]}.{parts[1]}" : $"{parts[0]}.{parts[1]}.{parts[2]}";
    }

    private static string? TopGameDataFolder(string file)
    {
        var parts = file.Split('/');
        return parts.Length >= 3 && parts[0].Equals("GameData", StringComparison.OrdinalIgnoreCase) ? parts[1] : null;
    }
}
