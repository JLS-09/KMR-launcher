using System;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using KMRLauncherMvvm.Models;
using KMRLauncherMvvm.Services;

namespace KMRLauncherMvvm.ViewModels.Instances;

public class InstanceListItemViewModel(Instance instance)
{
    public Instance Instance { get; } = instance;
    public string Name => Instance.Name;
    public string Version => Instance.Version;
    public bool IsPrimary => Instance.IsPrimary;
    public string ModsText => $"{Instance.Mods.Count} mods";
    public string LastPlayedText => InstanceTexts.LastPlayed(Instance);
    public bool IsMissing => Instance.IsMissing;
}

public partial class InstalledModRowViewModel : ObservableObject
{
    public ModVersion Version { get; }
    public string Name { get; }
    public string Reason { get; }
    public bool IsDependency { get; }
    public ModVersion? NextVersion { get; }
    public bool HasUpdate => NextVersion is not null;
    public ModVersion? Replacement { get; }
    public bool HasReplacement => Replacement is not null;
    public bool HasUpdateOrReplacement => HasUpdate || HasReplacement;

    public string ReplacementTip => Replacement is null
        ? ""
        : $"Replaced by {Replacement.Identifier} {Replacement.Version}. Replacing installs it and removes {Version.Identifier}.";

    public string ReplaceHeader => Replacement is null ? "" : $"Replace with {Replacement.Identifier}";
    public string NextText { get; }
    public bool IsNotListed { get; }
    public string NotListedText { get; }

    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private string _sizeText = "";

    public event Action? SelectionChanged;

    public InstalledModRowViewModel(Instance instance, ModVersion version, Mod? mod, GameVersionCriteria criteria,
        ModVersion? replacement = null)
    {
        Replacement = replacement;
        Version = version;
        Name = mod?.Name ?? version.Identifier;
        Reason = InstalledMods.ReasonText(instance, version);
        IsDependency = !InstalledMods.IsUserRequested(instance, version.Identifier);

        if (mod is not null && replacement is null)
        {
            var fit = Discover.ModFit.For(mod, instance, criteria);
            if (fit.Kind == Controls.ModStatusKind.Update) NextVersion = fit.NewestFit;
        }

        NextText = NextVersion is null ? "" : InstalledMods.ShortNextVersion(version.Version, NextVersion.Version);
        IsNotListed = version.Kind is null or "package" && !CompatibilityService.IsCompatible(version, criteria);
        NotListedText = $"Not listed for {instance.Version}";
    }

    partial void OnIsSelectedChanged(bool value) => SelectionChanged?.Invoke();
}

public static class InstanceTexts
{
    public static string LastPlayed(Instance instance)
    {
        if (instance.PlayTime <= 0 || instance.LastPlayed == default) return "Never";

        var ago = DateTime.Now - instance.LastPlayed;
        if (ago.TotalMinutes < 1) return "Just now";
        if (ago.TotalHours < 1) return $"{(int)ago.TotalMinutes} min ago";
        if (instance.LastPlayed.Date == DateTime.Today) return $"{(int)ago.TotalHours} h ago";
        if (instance.LastPlayed.Date == DateTime.Today.AddDays(-1)) return "Yesterday";
        if (ago.TotalDays < 7) return $"{(int)Math.Ceiling(ago.TotalDays)} days ago";
        return instance.LastPlayed.ToString("d MMM", CultureInfo.InvariantCulture);
    }

    public static string LastPlayedDetail(Instance instance)
    {
        if (instance.PlayTime <= 0 || instance.LastPlayed == default) return "Not played yet";

        var day = instance.LastPlayed.Date == DateTime.Today ? "Today"
            : instance.LastPlayed.Date == DateTime.Today.AddDays(-1) ? "Yesterday"
            : instance.LastPlayed.ToString("d MMM", CultureInfo.InvariantCulture);
        return $"{day}, {instance.LastPlayed:HH:mm}";
    }

    public static string PlayTime(Instance instance)
    {
        var time = TimeSpan.FromSeconds(Math.Max(0, instance.PlayTime));
        var hours = (int)time.TotalHours;
        return hours > 0 ? $"{hours}h {time.Minutes}m" : $"{time.Minutes}m";
    }

    public static string ShortPath(string path)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return !string.IsNullOrEmpty(home) && path.StartsWith(home) ? "~" + path[home.Length..] : path;
    }

    public static string Compatible(Instance instance)
    {
        var summary = KspVersions.DescribeCompatible(instance);
        var separator = summary.IndexOf(" · ", StringComparison.Ordinal);
        return separator < 0 ? $"Compatible: {instance.Version} only" : $"Compatible: {summary[(separator + 3)..]}";
    }
}