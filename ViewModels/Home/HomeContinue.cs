using System;
using System.Collections.Generic;
using System.Linq;
using KMRLauncherMvvm.Models;
using KMRLauncherMvvm.Services;
using KMRLauncherMvvm.ViewModels.Instances;

namespace KMRLauncherMvvm.ViewModels.Home;

public sealed class HomeContinue
{
    public Instance Instance { get; }
    public string Name { get; }
    public string KspText { get; }
    public bool IsPrimary { get; }
    public string LastPlayed { get; }
    public string LastPlayedDetail { get; }
    public string PlayTime { get; }
    public string ModsCount { get; }
    public string ModsDetail { get; }
    public string ReadyText { get; }
    public string UpdatesText { get; }
    public bool HasUpdates { get; }
    public string NotListedText { get; }
    public bool HasNotListed { get; }
    public ModsFilter ReviewFilter { get; }

    public HomeContinue(Instance instance, InstanceCheckResult? checks)
    {
        Instance = instance;
        Name = instance.Name;
        KspText = $"KSP {instance.Version}";
        IsPrimary = instance.IsPrimary;
        LastPlayed = InstanceTexts.LastPlayed(instance);
        LastPlayedDetail = InstanceTexts.LastPlayedDetail(instance);
        PlayTime = InstanceTexts.PlayTime(instance);

        var total = instance.Mods.Count;
        var picked = instance.Mods.Count(m => InstalledMods.IsUserRequested(instance, m.Identifier));
        ModsCount = total.ToString();
        ModsDetail = $"{picked} picked · {total - picked} dependencies";

        HashSet<string> installed = [.. instance.Mods.Select(m => m.Identifier)];
        HashSet<string> updates = [.. (checks?.Updates.Select(u => u.Identifier) ?? [])
            .Concat(checks?.Replacements.Select(r => r.Identifier) ?? []).Where(installed.Contains)];
        HashSet<string> notListed = [.. (checks?.NotListed ?? []).Where(installed.Contains)];
        var ready = installed.Count(id => !updates.Contains(id) && !notListed.Contains(id));

        ReadyText = Count(ready, "mod ready", "mods ready");
        HasUpdates = updates.Count > 0;
        UpdatesText = Count(updates.Count, "update", "updates");
        HasNotListed = notListed.Count > 0;
        NotListedText = $"{notListed.Count} not listed for {instance.Version}";
        ReviewFilter = HasUpdates ? ModsFilter.Updates : HasNotListed ? ModsFilter.Issues : ModsFilter.All;
    }

    public static Instance? Pick(IEnumerable<Instance> usable, Instance? primary) =>
        usable.Where(i => i.PlayTime > 0 && i.LastPlayed != default).MaxBy(i => i.LastPlayed) ?? primary;

    private static string Count(int count, string one, string many) => $"{count} {(count == 1 ? one : many)}";
}
