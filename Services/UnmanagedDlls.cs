using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using KMRLauncherMvvm.Models;

namespace KMRLauncherMvvm.Services;

public static partial class UnmanagedDlls
{
    private static readonly string[] StockFolders = ["GameData/Squad/", "GameData/SquadExpansion/"];

    [GeneratedRegex(@"^(?:.*/)?(?<identifier>[^.]+).*\.dll$", RegexOptions.IgnoreCase)]
    private static partial Regex DllPattern();

    [GeneratedRegex(@"^[^A-Za-z0-9]+")]
    private static partial Regex InvalidPrefix();

    [GeneratedRegex(@"[^A-Za-z0-9-]")]
    private static partial Regex InvalidCharacter();

    public static void Refresh(Instance instance) => instance.UnmanagedDlls = Scan(instance);

    public static IReadOnlyDictionary<string, string> Active(Instance? instance,
        IEnumerable<PlannedModVersion> requested)
    {
        if (instance is null || instance.UnmanagedDlls.Count == 0) return new Dictionary<string, string>();

        var installing = requested.Where(r => r.IsUserRequested).Select(r => r.Version.Identifier).ToHashSet();
        return instance.UnmanagedDlls.Where(d => !installing.Contains(d.Key)).ToDictionary();
    }

    public static Dictionary<string, string> Scan(Instance instance)
    {
        var gameData = Path.Combine(instance.RootPath, "GameData");
        if (!Directory.Exists(gameData)) return [];

        try
        {
            var files = Directory.EnumerateFiles(gameData, "*", SearchOption.AllDirectories)
                .Where(f => f.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                .Select(f => Path.GetRelativePath(instance.RootPath, f).Replace('\\', '/'));
            return Detect(files, instance);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    public static Dictionary<string, string> Detect(IEnumerable<string> dllPaths, Instance instance)
    {
        var owned = instance.ModRecords.Values.SelectMany(r => r.Files).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var installed = instance.Mods.Select(m => m.Identifier).ToHashSet();
        Dictionary<string, string> found = [];

        foreach (var path in dllPaths.Order(StringComparer.Ordinal))
        {
            if (StockFolders.Any(f => path.StartsWith(f, StringComparison.OrdinalIgnoreCase))) continue;
            if (owned.Contains(path) || IdentifierOf(path) is not { } identifier) continue;
            if (installed.Contains(identifier)) continue;
            found.TryAdd(identifier, path);
        }

        return found;
    }

    public static string? IdentifierOf(string relativePath)
    {
        if (!relativePath.StartsWith("GameData/", StringComparison.OrdinalIgnoreCase)) return null;

        var match = DllPattern().Match(relativePath);
        if (!match.Success) return null;

        var identifier = InvalidCharacter().Replace(InvalidPrefix().Replace(match.Groups["identifier"].Value, ""), "-");
        return identifier.Length == 0 ? null : identifier;
    }
}
