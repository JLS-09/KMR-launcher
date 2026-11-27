using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using KMRLauncherMvvm.Models;
using KMRLauncherMvvm.Services;

namespace KMRLauncherMvvm.ViewModels.Home;

public enum FeedTab
{
    YourMods,
    AllUpdates,
    NewMods
}

public sealed record FeedUpdate(Instance Instance, ModVersion Installed, ModVersion Target);

public sealed record FeedRow(string Name, string When, string From, string To, IReadOnlyList<string> Instances,
    IReadOnlyList<FeedUpdate> Updates, DateTime? Released, string Abstract = "")
{
    public bool HasFrom => From.Length > 0;
    public bool CanUpdate => Updates.Count > 0;
    public bool IsNewMod => !HasFrom;
}

public sealed class HomeCkanFeed
{
    public static readonly HomeCkanFeed Empty = new(("", ""), false, [], [], []);

    public string CommitsText { get; }
    public string TimesText { get; }
    public bool HasChanges { get; }
    public IReadOnlyList<FeedRow> YourMods { get; }
    public IReadOnlyList<FeedRow> AllUpdates { get; }
    public IReadOnlyList<FeedRow> NewMods { get; }

    private HomeCkanFeed((string Commits, string Times) subtitle, bool hasChanges, IReadOnlyList<FeedRow> yourMods,
        IReadOnlyList<FeedRow> allUpdates, IReadOnlyList<FeedRow> newMods)
    {
        (CommitsText, TimesText) = subtitle;
        HasChanges = hasChanges;
        YourMods = yourMods;
        AllUpdates = allUpdates;
        NewMods = newMods;
    }

    public IReadOnlyList<FeedRow> Rows(FeedTab tab) => tab switch
    {
        FeedTab.YourMods => YourMods,
        FeedTab.AllUpdates => AllUpdates,
        _ => NewMods
    };

    public static HomeCkanFeed Build(CkanChanges? changes, IReadOnlyDictionary<string, Mod> mods,
        IReadOnlyList<Instance> instances, HomeData data, DateTimeOffset now)
    {
        if (changes is null) return Empty;

        var names = ShortNames(instances);
        var checks = data.Instances.ToDictionary(r => r.Folder);
        List<FeedRow> yourMods = [];
        List<FeedRow> allUpdates = [];

        foreach (var group in changes.NewVersions.GroupBy(f => f.Identifier))
        {
            if (!mods.TryGetValue(group.Key, out var mod)) continue;

            var added = group.Select(f => f.Id).ToHashSet();
            var newest = mod.Versions.FirstOrDefault(v => v.Id is not null && added.Contains(v.Id));
            if (newest is null) continue;

            var previous = mod.Versions.FirstOrDefault(v => v.Id is null || !added.Contains(v.Id));
            var installedIn = instances.Where(i => i.Mods.Exists(m => m.Identifier == mod.Id)).ToList();
            List<FeedUpdate> updates =
            [
                .. installedIn.Select(i => Update(i, mod, checks.GetValueOrDefault(i.RootPath))).OfType<FeedUpdate>()
            ];

            allUpdates.Add(new FeedRow(mod.Name, When(newest, now), previous?.Version ?? "–", newest.Version,
                [.. installedIn.Select(i => names[i])], updates, newest.ReleaseDate));
            if (updates.Count > 0)
                yourMods.Add(new FeedRow(mod.Name, When(newest, now), updates[0].Installed.Version,
                    updates[0].Target.Version, [.. updates.Select(u => names[u.Instance])], updates,
                    newest.ReleaseDate));
        }

        List<FeedRow> newMods =
        [
            .. changes.NewMods.GroupBy(f => f.Identifier)
                .Select(g => mods.GetValueOrDefault(g.Key))
                .OfType<Mod>()
                .Where(m => m.Versions.Count > 0)
                .Select(m => new FeedRow(m.Name, When(m.Versions[0], now), "", m.Versions[0].Version, [], [],
                    m.Versions[0].ReleaseDate, m.Abstract))
        ];

        return new HomeCkanFeed(SubtitleFor(changes, now), true, Sorted(yourMods), Sorted(allUpdates), Sorted(newMods));
    }

    private static FeedUpdate? Update(Instance instance, Mod mod, InstanceCheckResult? check)
    {
        if (check?.Updates.FirstOrDefault(u => u.Identifier == mod.Id) is not { } update) return null;

        var installed = instance.Mods.First(m => m.Identifier == mod.Id);
        var target = mod.Versions.FirstOrDefault(v => v.Version == update.Version);
        return target is null ? null : new FeedUpdate(instance, installed, target);
    }

    private static List<FeedRow> Sorted(List<FeedRow> rows) =>
        [.. rows.OrderByDescending(r => r.Released ?? DateTime.MinValue).ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)];

    private static string When(ModVersion version, DateTimeOffset now) => version.ReleaseDate is { } date
        ? SyncStatusService.TimeAgo(new DateTimeOffset(DateTime.SpecifyKind(date, DateTimeKind.Utc)), now)
        : "";

    public static (string Commits, string Times) SubtitleFor(CkanChanges changes, DateTimeOffset now)
    {
        var commits = $"{Short(changes.FromCommit)} → {Short(changes.ToCommit)}";
        var to = SyncStatusService.TimeAgo(changes.ToSync, now);
        return changes.FromSync is { } from
            ? (commits, $" · {SyncTime(from.ToOffset(now.Offset), now)} → {to}")
            : (commits, $" · synced {to}");
    }

    private static string Short(string commit) => commit.Length > 7 ? commit[..7] : commit;

    private static string SyncTime(DateTimeOffset time, DateTimeOffset now)
    {
        var day = time.Date == now.Date ? "today"
            : time.Date == now.Date.AddDays(-1) ? "yesterday"
            : time.ToString("d MMM", CultureInfo.InvariantCulture);
        return $"{day} {time:HH:mm}";
    }

    public static Dictionary<Instance, string> ShortNames(IReadOnlyList<Instance> instances)
    {
        static string First(Instance i) => i.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? i.Name;
        var taken = instances.GroupBy(First).Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet();
        return instances.ToDictionary(i => i, i => taken.Contains(First(i)) ? i.Name : First(i));
    }
}
