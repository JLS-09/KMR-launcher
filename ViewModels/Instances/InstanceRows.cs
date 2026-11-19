using System;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using KMRLauncherMvvm.Models;
using KMRLauncherMvvm.Services;

namespace KMRLauncherMvvm.ViewModels.Instances;

// One instance in the list on the left of the Instances page
public class InstanceListItemViewModel(Instance instance)
{
    public Instance Instance { get; } = instance;
    public string Name => Instance.Name;
    public string Version => Instance.Version;
    public bool IsPrimary => Instance.IsPrimary;
    public string ModsText => $"{Instance.Mods.Count} mods";
    public string LastPlayedText => InstanceTexts.LastPlayed(Instance);
}

// One installed mod on the Mods tab
public partial class InstalledModRowViewModel : ObservableObject
{
    public ModVersion Version { get; }
    public string Name { get; }
    public string Reason { get; }
    public bool IsDependency { get; }
    public ModVersion? NextVersion { get; }
    public bool HasUpdate => NextVersion is not null;
    public string NextText { get; }
    public bool IsNotListed { get; }
    public string NotListedText { get; }

    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private string _sizeText = "";

    public event Action? SelectionChanged;

    public InstalledModRowViewModel(Instance instance, ModVersion version, Mod? mod, GameVersionCriteria criteria)
    {
        Version = version;
        Name = mod?.Name ?? version.Identifier;
        Reason = InstalledMods.ReasonText(instance, version);
        IsDependency = !InstalledMods.IsUserRequested(instance, version.Identifier);

        if (mod is not null)
        {
            var fit = Discover.ModFit.For(mod, instance, criteria);
            if (fit.Kind == Controls.ModStatusKind.Update) NextVersion = fit.NewestFit;
        }

        NextText = NextVersion is null ? "" : InstalledMods.ShortNextVersion(version.Version, NextVersion.Version);
        // Metapackages and DLC have no KSP versions worth flagging
        IsNotListed = version.Kind is null or "package" && !CompatibilityService.IsCompatible(version, criteria);
        NotListedText = $"Not listed for {instance.Version}";
    }

    partial void OnIsSelectedChanged(bool value) => SelectionChanged?.Invoke();
}

public static class InstanceTexts
{
    // "2 h ago", "Yesterday", "5 days ago", "3 Oct", "Never"
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

    // "Today, 19:02", "Yesterday, 21:40", "3 Oct, 19:02"
    public static string LastPlayedDetail(Instance instance)
    {
        if (instance.PlayTime <= 0 || instance.LastPlayed == default) return "Not played yet";

        var day = instance.LastPlayed.Date == DateTime.Today ? "Today"
            : instance.LastPlayed.Date == DateTime.Today.AddDays(-1) ? "Yesterday"
            : instance.LastPlayed.ToString("d MMM", CultureInfo.InvariantCulture);
        return $"{day}, {instance.LastPlayed:HH:mm}";
    }

    // "41h 20m", "12m", "0m"
    public static string PlayTime(Instance instance)
    {
        var time = TimeSpan.FromSeconds(Math.Max(0, instance.PlayTime));
        var hours = (int)time.TotalHours;
        return hours > 0 ? $"{hours}h {time.Minutes}m" : $"{time.Minutes}m";
    }

    // "~/KMR/instances/rp1-express"
    public static string ShortPath(string path)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return !string.IsNullOrEmpty(home) && path.StartsWith(home) ? "~" + path[home.Length..] : path;
    }

    // "Compatible: 1.12.x" (what the instance accepts besides its own version)
    public static string Compatible(Instance instance)
    {
        var summary = KspVersions.DescribeCompatible(instance);
        var separator = summary.IndexOf(" · ", StringComparison.Ordinal);
        return separator < 0 ? $"Compatible: {instance.Version} only" : $"Compatible: {summary[(separator + 3)..]}";
    }
}
