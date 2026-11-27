using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using KMRLauncherMvvm.Models;

namespace KMRLauncherMvvm.Services;

public record RemovalPlan(
    IReadOnlyList<ModVersion> Selected,
    IReadOnlyList<ModVersion> Dependents,
    IReadOnlyList<ModVersion> Orphans)
{
    public IEnumerable<ModVersion> All => Selected.Concat(Dependents).Concat(Orphans);
}

public static class InstalledMods
{
    public static bool IsUserRequested(Instance instance, string identifier) =>
        !instance.ModRecords.TryGetValue(identifier, out var record) || record.IsUserRequested;

    public static bool IsSatisfied(Relationship relationship, IEnumerable<ModVersion> mods,
        IReadOnlyDictionary<string, string>? dlls = null)
    {
        return dlls is not null && Relationships.NamesIn(relationship).Any(dlls.ContainsKey) ||
               mods.Any(m => Relationships.Names(relationship, m));
    }

    public static List<ModVersion> Dependents(ModVersion mod, IEnumerable<ModVersion> installed)
    {
        return
        [
            .. installed.Where(m => m.Identifier != mod.Identifier &&
                                    (m.Depends ?? []).Any(r => Relationships.Names(r, mod)))
        ];
    }

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
                var others = remaining.Where(m => m != mod).ToList();
                if ((mod.Depends ?? []).All(r => IsSatisfied(r, others, instance.UnmanagedDlls) ||
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

    public static string DescribeRemoval(RemovalPlan plan)
    {
        List<string> parts = [];
        if (plan.Dependents.Count > 0)
            parts.Add(
                $"{JoinNames(plan.Dependents)}, which {(plan.Dependents.Count == 1 ? "depends" : "depend")} on it");
        if (plan.Orphans.Count > 0)
            parts.Add($"{JoinNames(plan.Orphans)}, which no other mod needs");

        return parts.Count == 0
            ? "No other mods depend on the selection."
            : $"Removing also removes {string.Join("; and ", parts)}.";
    }

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

    public static string FormatSize(long bytes)
    {
        var mb = bytes / 1024.0 / 1024.0;
        if (bytes > 0 && mb < 0.1) mb = 0.1;
        if (mb >= 1024) return (mb / 1024).ToString("0.0", CultureInfo.InvariantCulture) + " GB";
        return mb >= 10
            ? Math.Round(mb).ToString(CultureInfo.InvariantCulture) + " MB"
            : mb.ToString("0.0", CultureInfo.InvariantCulture) + " MB";
    }

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

    public static PlannedModVersion PlanUpdate(Instance instance, ModVersion installed, ModVersion target,
        bool replaces = false) => new()
    {
        Version = target,
        IsUserRequested = IsUserRequested(instance, installed.Identifier),
        IsVersionPickedByUser = true,
        OptionalReason = instance.ModRecords.GetValueOrDefault(installed.Identifier)?.Reason,
        Replaces = replaces ? installed.Identifier : null
    };

    public static ModVersion? FindReplacement(Instance instance, ModVersion installed,
        IReadOnlyDictionary<string, Mod> mods, GameVersionCriteria criteria)
    {
        if (!mods.TryGetValue(installed.Identifier, out var mod) || mod.Versions.Count == 0) return null;

        var newest = mod.Versions.Aggregate((a, b) => CkanVersion.Compare(a.Version, b.Version) >= 0 ? a : b);
        if (newest.ReplacedBy is not { Name: { } name } replacedBy) return null;
        if (instance.Mods.Exists(m => m.Identifier == name)) return null;
        if (!mods.TryGetValue(name, out var replacement)) return null;

        return replacement.Versions
            .Where(v => KspCompatibility.IsCompatible(v, criteria))
            .Where(v => replacedBy.Version is null || CkanVersion.Compare(v.Version, replacedBy.Version) == 0)
            .Where(v => replacedBy.MinVersion is null || CkanVersion.Compare(v.Version, replacedBy.MinVersion) >= 0)
            .OrderByDescending(v => v.Version, Comparer<string>.Create(CkanVersion.Compare))
            .FirstOrDefault();
    }
}