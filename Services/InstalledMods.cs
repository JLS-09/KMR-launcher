using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using KMRLauncherMvvm.Models;

namespace KMRLauncherMvvm.Services;

// What a removal also takes with it (Phase 5 task 2)
public record RemovalPlan(IReadOnlyList<ModVersion> Selected, IReadOnlyList<ModVersion> Dependents,
    IReadOnlyList<ModVersion> Orphans)
{
    public IEnumerable<ModVersion> All => Selected.Concat(Dependents).Concat(Orphans);
}

// Logic for an instance's installed mods: reasons, removal, sizes. Only depends count, like CKAN's
// auto-removal; version limits are ignored (a mod that is installed satisfies a name).
public static class InstalledMods
{
    // Picked by the user (or ticked as a recommendation). A mod without a record (installed before records
    // existed) counts as picked, so it is never removed as an unused dependency.
    public static bool IsUserRequested(Instance instance, string identifier) =>
        !instance.ModRecords.TryGetValue(identifier, out var record) || record.IsUserRequested;

    // The names a mod can be depended on by: its identifier and what it provides
    public static IEnumerable<string> NamesOf(ModVersion mod) => [mod.Identifier, .. mod.Provides ?? []];

    // All names a relationship accepts (the name, or every any_of option)
    public static IEnumerable<string> NamesIn(Relationship relationship) =>
        relationship.AnyOf?.Select(e => e.Name).OfType<string>() ??
        (relationship.Name is null ? [] : [relationship.Name]);

    public static bool IsSatisfied(Relationship relationship, IEnumerable<ModVersion> mods)
    {
        var names = NamesIn(relationship).ToHashSet();
        return mods.Any(m => NamesOf(m).Any(names.Contains));
    }

    // Installed mods whose depends name this mod (by identifier or a provided name)
    public static List<ModVersion> Dependents(ModVersion mod, IEnumerable<ModVersion> installed)
    {
        var names = NamesOf(mod).ToHashSet();
        return
        [
            .. installed.Where(m => m.Identifier != mod.Identifier &&
                                    (m.Depends ?? []).Any(r => NamesIn(r).Any(names.Contains)))
        ];
    }

    // Removing the selection also removes (1) installed mods that depend on it and have nothing else left to
    // satisfy that dependency, and (2) dependencies nobody picked that no remaining mod needs. Both repeat until
    // nothing changes.
    public static RemovalPlan PlanRemoval(Instance instance, IReadOnlyCollection<string> selected)
    {
        var remaining = instance.Mods.Where(m => !selected.Contains(m.Identifier)).ToList();
        List<ModVersion> dependents = [];
        List<ModVersion> orphans = [];

        bool changed;
        do
        {
            changed = false;

            foreach (var mod in remaining.ToList())
            {
                // Only depends the removal breaks count: one that wasn't satisfied before (a missing mod, a DLC)
                // isn't a reason to remove anything
                var others = remaining.Where(m => m != mod).ToList();
                if ((mod.Depends ?? []).All(r => IsSatisfied(r, others) ||
                                                 !IsSatisfied(r, instance.Mods.Where(m => m != mod))))
                    continue;
                remaining.Remove(mod);
                dependents.Add(mod);
                changed = true;
            }

            foreach (var mod in remaining.ToList())
            {
                if (IsUserRequested(instance, mod.Identifier)) continue;
                if (Dependents(mod, remaining).Count > 0) continue;
                remaining.Remove(mod);
                orphans.Add(mod);
                changed = true;
            }
        } while (changed);

        return new RemovalPlan([.. instance.Mods.Where(m => selected.Contains(m.Identifier))], dependents, orphans);
    }

    // The line in the selection bar: what else goes when the selection is removed
    public static string DescribeRemoval(RemovalPlan plan)
    {
        List<string> parts = [];
        if (plan.Dependents.Count > 0)
            parts.Add($"{JoinNames(plan.Dependents)}, which {(plan.Dependents.Count == 1 ? "depends" : "depend")} on it");
        if (plan.Orphans.Count > 0)
            parts.Add($"{JoinNames(plan.Orphans)}, which no other mod needs");

        return parts.Count == 0
            ? "No other mods depend on the selection."
            : $"Removing also removes {string.Join("; and ", parts)}.";
    }

    // "Picked by you", "Recommended by X", "Dependency of X", "Dependency of X and 4 more", "Dependency of 31 mods"
    public static string ReasonText(Instance instance, ModVersion mod)
    {
        instance.ModRecords.TryGetValue(mod.Identifier, out var record);
        if (record is null || record.IsUserRequested) return record?.Reason ?? "Picked by you";

        var dependents = Dependents(mod, instance.Mods);
        return dependents.Count switch
        {
            0 => "Dependency, no mod needs it anymore",
            1 => $"Dependency of {dependents[0].Identifier}",
            <= 5 => $"Dependency of {dependents[0].Identifier} and {dependents.Count - 1} more",
            _ => $"Dependency of {dependents.Count} mods"
        };
    }

    // "412 MB", "1.1 MB", "0.2 MB", "6.8 GB"
    public static string FormatSize(long bytes)
    {
        var mb = bytes / 1024.0 / 1024.0;
        if (mb >= 1024) return (mb / 1024).ToString("0.0", CultureInfo.InvariantCulture) + " GB";
        return mb >= 10
            ? Math.Round(mb).ToString(CultureInfo.InvariantCulture) + " MB"
            : mb.ToString("0.0", CultureInfo.InvariantCulture) + " MB";
    }

    // The update badge: long versions only show the part that changes ("release-1.12.1-200" → "-201")
    public static string ShortNextVersion(string installed, string next)
    {
        if (next.Length <= 12) return next;

        var common = 0;
        while (common < installed.Length && common < next.Length && installed[common] == next[common]) common++;
        var cut = next.LastIndexOf('-', Math.Max(0, Math.Min(common, next.Length - 1)));
        return cut > 0 ? next[cut..] : next;
    }

    private static string JoinNames(IReadOnlyList<ModVersion> mods) => mods.Count switch
    {
        1 => mods[0].Identifier,
        2 => $"{mods[0].Identifier} and {mods[1].Identifier}",
        <= 4 => $"{string.Join(", ", mods.Take(mods.Count - 1).Select(m => m.Identifier))} and {mods[^1].Identifier}",
        _ => $"{mods[0].Identifier}, {mods[1].Identifier} and {mods.Count - 2} more"
    };
}
