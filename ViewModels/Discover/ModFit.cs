using System;
using System.Collections.Generic;
using System.Linq;
using KMRLauncherMvvm.Controls;
using KMRLauncherMvvm.Models;
using KMRLauncherMvvm.Services;

namespace KMRLauncherMvvm.ViewModels.Discover;

public record ModFit(ModStatusKind? Kind, string Text, ModVersion? NewestFit, ModVersion? Installed)
{
    public static readonly ModFit None = new(null, "", null, null);

    public static ModFit For(Mod mod, Instance? instance, GameVersionCriteria? criteria)
    {
        if (instance is null || criteria is null) return None;

        var installed = instance.Mods.FirstOrDefault(v => v.Identifier == mod.Id);
        var newestFit = mod.Versions.FirstOrDefault(v => CompatibilityService.IsCompatible(v, criteria));

        if (installed is not null)
        {
            return newestFit is not null && CkanVersion.Compare(newestFit.Version, installed.Version) > 0
                ? new ModFit(ModStatusKind.Update, "Update", newestFit, installed)
                : new ModFit(ModStatusKind.Installed, "Installed", newestFit, installed);
        }

        return newestFit is not null
            ? new ModFit(ModStatusKind.Fits, "Fits", newestFit, null)
            : new ModFit(ModStatusKind.Warning, $"Not {instance.Version}", null, null);
    }

    public bool IsCompatible => NewestFit is not null;
    public bool IsInstalled => Installed is not null;
}

public static class DiscoverFilter
{
    public const string PackageKind = "Packages";
    public const string MetapackageKind = "Metapackages";

    public static bool Matches(Mod mod, ModFit fit, string search, IReadOnlyCollection<string> tags,
        IReadOnlyCollection<string> kinds, bool onlyCompatible, bool hideInstalled)
    {
        if (onlyCompatible && fit != ModFit.None && !fit.IsCompatible) return false;
        if (hideInstalled && fit.IsInstalled) return false;

        if (search.Length > 0 &&
            !mod.Name.Contains(search, StringComparison.OrdinalIgnoreCase) &&
            !mod.Id.Contains(search, StringComparison.OrdinalIgnoreCase) &&
            !mod.Author.Any(a => a.Contains(search, StringComparison.OrdinalIgnoreCase)))
            return false;

        if (tags.Count > 0 && (mod.Tags is null || !mod.Tags.Any(tags.Contains))) return false;

        if (kinds.Count > 0 && !kinds.Contains(KindOf(mod))) return false;

        return true;
    }

    public static string KindOf(Mod mod) => mod.LatestVersion?.Kind switch
    {
        "metapackage" => MetapackageKind,
        "dlc" => "DLC",
        _ => PackageKind
    };

    public static Dictionary<string, int> CountNeededBy(IEnumerable<Mod> mods)
    {
        Dictionary<string, int> counts = [];

        foreach (var mod in mods)
        {
            var names = new HashSet<string>();
            foreach (var relationship in mod.LatestVersion?.Depends ?? [])
            {
                if (relationship.Name is not null) names.Add(relationship.Name);
                foreach (var entry in relationship.AnyOf ?? [])
                    if (entry.Name is not null)
                        names.Add(entry.Name);
            }

            names.Remove(mod.Id);
            foreach (var name in names)
                counts[name] = counts.GetValueOrDefault(name) + 1;
        }

        return counts;
    }

    public static string FormatDownloads(long? count) => count switch
    {
        null => "–",
        >= 1_000_000 => (count.Value / 1_000_000.0).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) +
                        "M",
        >= 1_000 => $"{Math.Round(count.Value / 1_000.0)}K",
        _ => count.Value.ToString()
    };

    public static string ShortAuthors(IReadOnlyList<string> authors) => authors.Count switch
    {
        0 => "",
        <= 2 => string.Join(", ", authors),
        _ => $"{authors[0]} +{authors.Count - 1}"
    };

    public static string LongAuthors(IReadOnlyList<string> authors) => authors.Count switch
    {
        0 => "",
        1 => $"by {authors[0]}",
        2 => $"by {authors[0]} and {authors[1]}",
        _ => $"by {authors[0]} and {authors.Count - 1} others"
    };
}