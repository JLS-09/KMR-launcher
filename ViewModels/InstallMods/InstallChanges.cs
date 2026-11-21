using System;
using System.Collections.Generic;
using System.Linq;
using KMRLauncherMvvm.Models;
using KMRLauncherMvvm.Services;

namespace KMRLauncherMvvm.ViewModels.InstallMods;

public enum ChangeKind
{
    New,
    Update,
    Remove
}

public sealed record ChangeRow(
    ChangeKind Kind,
    string Name,
    string Reason,
    string Version,
    string? OldVersion,
    long? Size,
    bool IsCached)
{
    public bool IsNew => Kind == ChangeKind.New;
    public bool IsUpdate => Kind == ChangeKind.Update;
    public bool IsRemove => Kind == ChangeKind.Remove;
    public string KindText => Kind.ToString();

    public string ChangeText => OldVersion is null
        ? Version
        : $"{OldVersion} → {InstalledMods.ShortNextVersion(OldVersion, Version)}";

    public string ReplacesText => OldVersion is null
        ? ""
        : $"replaces {InstalledMods.ShortNextVersion(Version, OldVersion)}";

    public string SizeText => IsRemove || IsCached || Size is null ? "" : InstalledMods.FormatSize(Size.Value);
    public bool ShowCached => IsCached && !IsRemove;
}

public enum FitKind
{
    Fits,
    Installed,
    Warning
}

public sealed record ChangeCounts(int New, int Updates, int Removed, long DownloadBytes, long CachedBytes)
{
    public long ToDownloadBytes => DownloadBytes - CachedBytes;
}

public static class InstallChanges
{
    public static List<ChangeRow> Rows(InstallModsData data, Func<ModVersion, bool> isCached)
    {
        var instance = data.SelectedInstance;
        var installed = instance?.Mods ?? [];
        var planned = data.FinalModList.ToList();
        var installIdentifiers = planned.Select(p => p.Version.Identifier).ToHashSet();

        var rows = planned.Select(p =>
        {
            var old = installed.FirstOrDefault(m => m.Identifier == p.Version.Identifier);
            return new ChangeRow(old is null ? ChangeKind.New : ChangeKind.Update, p.Version.Identifier,
                Reason(p, instance, planned), p.Version.Version, old?.Version, p.Version.DownloadSize,
                NeedsDownload(p.Version) && isCached(p.Version));
        }).ToList();

        rows.AddRange(data.RequestedRemoveModVersions
            .Where(r => !installIdentifiers.Contains(r.Version.Identifier))
            .Select(r => new ChangeRow(ChangeKind.Remove, r.Version.Identifier,
                planned.FirstOrDefault(p => p.Replaces == r.Version.Identifier) is { } by
                    ? $"Replaced by {by.Version.Identifier}"
                    : "Removed from the instance",
                r.Version.Version, null, null, false)));

        return
        [
            .. rows.OrderBy(r => r.Kind switch { ChangeKind.Update => 0, ChangeKind.New => 1, _ => 2 })
                .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
        ];
    }

    public static ChangeCounts Count(IReadOnlyList<ChangeRow> rows) => new(
        rows.Count(r => r.IsNew),
        rows.Count(r => r.IsUpdate),
        rows.Count(r => r.IsRemove),
        rows.Where(r => !r.IsRemove).Sum(r => r.Size ?? 0),
        rows.Where(r => r.IsCached).Sum(r => r.Size ?? 0));

    public static string Reason(PlannedModVersion planned, Instance? instance, IReadOnlyList<PlannedModVersion> all)
    {
        if (planned.Replaces is { } replaces) return $"Replaces {replaces}";
        if (planned.OptionalReason is { } reason) return reason;
        if (planned.IsUserRequested) return "Picked by you";

        if (planned.RequiredBy.Count > 0)
        {
            var names = planned.RequiredBy.Select(id => DisplayName(id, all, withVersion: false)).Distinct().ToList();
            return "Dependency of " + JoinNames(names);
        }

        var installed = instance?.Mods.FirstOrDefault(m => m.Identifier == planned.Version.Identifier);
        return installed is not null && instance is not null
            ? InstalledMods.ReasonText(instance, installed)
            : "Dependency";
    }

    public static string DisplayName(string id, IEnumerable<PlannedModVersion> known, bool withVersion)
    {
        var version = known.Select(p => p.Version).FirstOrDefault(v => v.Id == id || v.Identifier == id);
        if (version is null) return id;
        return withVersion ? $"{version.Identifier} {version.Version}" : version.Identifier;
    }

    public static string JoinNames(IReadOnlyList<string> names) => names.Count switch
    {
        0 => "",
        1 => names[0],
        2 => $"{names[0]} and {names[1]}",
        _ => $"{names[0]} and {names.Count - 1} more"
    };

    public static string JoinAll(IReadOnlyList<string> names) => names.Count <= 1
        ? string.Join("", names)
        : $"{string.Join(", ", names.Take(names.Count - 1))} and {names[^1]}";

    public static (string Text, FitKind Kind) FitHint(Instance instance, IReadOnlyList<PlannedModVersion> requested)
    {
        var criteria = GameVersionCriteria.FromInstance(instance);
        var notFitting = requested.Where(r => !CompatibilityService.IsCompatible(r.Version, criteria)).ToList();
        if (notFitting.Count > 0)
        {
            var more = notFitting.Count > 1 ? $" and {notFitting.Count - 1} more" : "";
            return ($"{notFitting[0].Version.Identifier}{more} not for {instance.Version}", FitKind.Warning);
        }

        var installed = requested.Count(r =>
            instance.Mods.Any(m => m.Identifier == r.Version.Identifier && m.Version == r.Version.Version));
        if (installed > 0) return ($"{installed} already installed", FitKind.Installed);

        return (requested.Count == 1 ? "Fits" : $"All {requested.Count} fit", FitKind.Fits);
    }

    public static bool NeedsDownload(ModVersion version) => version.Kind is null or "package";

    public static string Plural(int count, string word) => count == 1 ? $"1 {word}" : $"{count} {word}s";

    public static string Duration(TimeSpan time) => time.TotalHours >= 1
        ? $"{(int)time.TotalHours} h {time.Minutes} min"
        : time.TotalMinutes >= 1
            ? $"{(int)time.TotalMinutes} min {time.Seconds} s"
            : $"{Math.Max(1, (int)Math.Round(time.TotalSeconds))} s";
}
