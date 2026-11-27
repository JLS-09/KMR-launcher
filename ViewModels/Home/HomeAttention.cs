using System.Collections.Generic;
using System.Linq;
using KMRLauncherMvvm.Models;
using KMRLauncherMvvm.Services;

namespace KMRLauncherMvvm.ViewModels.Home;

public enum AttentionKind
{
    Updates,
    NotListed,
    Replacements,
    UnusedDownloads
}

public sealed record AttentionRow(AttentionKind Kind, string Title, string Detail, string ButtonText,
    Instance? Target = null, ModsFilter Filter = ModsFilter.All)
{
    public bool IsUpdates => Kind == AttentionKind.Updates;
    public bool IsNotListed => Kind == AttentionKind.NotListed;
    public bool IsReplacements => Kind == AttentionKind.Replacements;
    public bool IsUnusedDownloads => Kind == AttentionKind.UnusedDownloads;
}

public static class HomeAttention
{
    public static List<AttentionRow> Rows(IReadOnlyList<Instance> instances, HomeData data)
    {
        var results = data.Instances.ToDictionary(r => r.Folder);
        var checks = instances
            .Select(i => (Instance: i, Result: results.GetValueOrDefault(i.RootPath)))
            .Where(c => c.Result is not null)
            .Select(c => (c.Instance, Result: c.Result!))
            .ToList();
        List<AttentionRow> rows = [];

        var updates = checks.Where(c => c.Result.Updates.Count > 0).ToList();
        var updateCount = updates.Sum(c => c.Result.Updates.Count);
        if (updateCount > 0)
            rows.Add(new AttentionRow(AttentionKind.Updates, Plural(updateCount, "mod update", "mod updates"),
                $"In {Join(updates.Select(c => c.Instance.Name).ToList())}", "Review", updates[0].Instance,
                ModsFilter.Updates));

        var notListed = checks.Where(c => c.Result.NotListed.Count > 0).ToList();
        var notListedCount = notListed.Sum(c => c.Result.NotListed.Count);
        if (notListedCount > 0)
        {
            var versions = notListed.Select(c => c.Instance.Version).Distinct().ToList();
            var forText = versions.Count == 1 ? $"KSP {versions[0]}" : "their KSP versions";
            var detail = notListed.Count == 1
                ? $"{Join(notListed[0].Result.NotListed, 2)} in {notListed[0].Instance.Name}"
                : $"In {Join(notListed.Select(c => c.Instance.Name).ToList())}";
            rows.Add(new AttentionRow(AttentionKind.NotListed,
                $"{Plural(notListedCount, "mod", "mods")} not listed for {forText}", detail, "Show",
                notListed[0].Instance, ModsFilter.Issues));
        }

        var replaced = checks.Where(c => c.Result.Replacements.Count > 0).ToList();
        var replacedCount = replaced.Sum(c => c.Result.Replacements.Count);
        if (replacedCount > 0)
            rows.Add(new AttentionRow(AttentionKind.Replacements,
                replacedCount == 1 ? "1 mod has a replacement" : $"{replacedCount} mods have a replacement",
                replacedCount == 1
                    ? "Its authors moved it to a new identifier"
                    : "Their authors moved them to new identifiers",
                "Show", replaced[0].Instance, ModsFilter.Updates));

        if (data.UnusedDownloads > 0)
            rows.Add(new AttentionRow(AttentionKind.UnusedDownloads,
                $"{InstalledMods.FormatSize(data.UnusedDownloadBytes)} of unused downloads",
                $"{Plural(data.UnusedDownloads, "cached archive", "cached archives")} no instance uses", "Clean up"));

        return rows;
    }

    private static string Plural(int count, string one, string many) => $"{count} {(count == 1 ? one : many)}";

    private static string Join(IReadOnlyList<string> names, int shown = 3) => names.Count switch
    {
        1 => names[0],
        _ when names.Count <= shown => $"{string.Join(", ", names.Take(names.Count - 1))} and {names[^1]}",
        _ => $"{string.Join(", ", names.Take(shown - 1))} and {names.Count - (shown - 1)} more"
    };
}
