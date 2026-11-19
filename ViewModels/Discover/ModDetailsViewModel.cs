using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KMRLauncherMvvm.Models;
using KMRLauncherMvvm.Services;

namespace KMRLauncherMvvm.ViewModels.Discover;

// The details panel on the right of Discover, for the row that was clicked
public partial class ModDetailsViewModel : ObservableObject
{
    private readonly ModListItemViewModel _row;
    private readonly Instance? _instance;

    public Mod Mod => _row.Mod;
    public string Authors { get; }
    public string License { get; }
    public IReadOnlyList<string> Tags { get; }
    public string Description { get; }

    public IReadOnlyList<VersionRowViewModel> Versions { get; }
    public string VersionCount { get; }
    public bool HasInstance => _instance is not null;
    public string FitsLegend { get; }

    [ObservableProperty] private ModVersion _shownVersion;
    [ObservableProperty] private bool _isStrict;
    [ObservableProperty] private IReadOnlyList<DependsRowViewModel> _depends = [];
    [ObservableProperty] private bool _hasDepends;
    [ObservableProperty] private string _conflictsText = "";
    [ObservableProperty] private bool _hasConflicts;
    public string NeededBy { get; }
    public string? InstanceVersion => _instance?.Version;

    public IReadOnlyList<LinkViewModel> Links { get; }
    public bool HasLinks => Links.Count > 0;

    public ModDetailsViewModel(ModListItemViewModel row, Instance? instance, GameVersionCriteria? criteria,
        int neededBy)
    {
        _row = row;
        _instance = instance;

        Authors = DiscoverFilter.LongAuthors(Mod.Author);
        var latest = Mod.LatestVersion;
        License = latest is null ? "" : string.Join(", ", latest.License);
        Tags = Mod.Tags ?? [];
        Description = string.IsNullOrWhiteSpace(Mod.Description) ? Mod.Abstract : Mod.Description!;
        NeededBy = neededBy.ToString("N0");

        Versions = [.. Mod.Versions.Select(v => new VersionRowViewModel(v, row,
            fits: criteria is not null && CompatibilityService.IsCompatible(v, criteria),
            isNewestFit: v == row.Fit.NewestFit,
            isInstalled: row.Fit.Installed?.Version == v.Version))];
        VersionCount = Mod.Versions.Count.ToString("N0");
        FitsLegend = instance is null ? "" : $"Fits {instance.Name} ({instance.Version})";

        Links = BuildLinks(Mod.Resources);

        _shownVersion = row.SelectedVersion;
        UpdateShownVersion();
        row.PropertyChanged += OnRowChanged;
    }

    // Called when the panel shows another mod, so the old row doesn't keep this one alive
    public void Detach() => _row.PropertyChanged -= OnRowChanged;

    private void OnRowChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ModListItemViewModel.IsSelected) or nameof(ModListItemViewModel.SelectedVersion))
            Refresh();
    }

    private void Refresh()
    {
        foreach (var version in Versions) version.Refresh();
        ShownVersion = _row.SelectedVersion;
        UpdateShownVersion();
    }

    // Depends, conflicts and the KSP strip follow the version that would be installed
    private void UpdateShownVersion()
    {
        var version = ShownVersion;
        IsStrict = version.KspVersionStrict;

        var installed = _instance?.Mods ?? [];
        Depends = [.. (version.Depends ?? []).Select(r => DependsRowViewModel.For(r, installed, _instance is not null))];
        HasDepends = Depends.Count > 0;

        if (_instance is null)
        {
            ConflictsText = "";
            HasConflicts = false;
            return;
        }

        var conflicts = CompatibilityService.FindConflicts([version], _instance.Mods);
        HasConflicts = conflicts.Count > 0;
        ConflictsText = conflicts.Count switch
        {
            0 => "No conflicts",
            1 => "Conflicts with 1 installed mod",
            _ => $"Conflicts with {conflicts.Count} installed mods"
        };
    }

    private static List<LinkViewModel> BuildLinks(Resources? resources)
    {
        List<LinkViewModel> links = [];
        if (resources is null) return links;

        void Add(string label, string? url)
        {
            if (!string.IsNullOrWhiteSpace(url)) links.Add(new LinkViewModel(label, url));
        }

        Add("Homepage", resources.Homepage);
        Add("Repository", resources.Repository);
        Add("Forum thread", resources.Discussions);
        Add("Bug tracker", resources.Bugtracker);
        Add("SpaceDock", resources.Spacedock);
        Add("Manual", resources.Manual);
        return links;
    }
}

// A row in the details panel's version list: ticking it selects the mod with that version (like CKAN)
public partial class VersionRowViewModel : ObservableObject
{
    private readonly ModListItemViewModel _row;

    public ModVersion Version { get; }
    public bool Fits { get; }
    public bool IsNewestFit { get; }
    public bool IsInstalled { get; }
    public string KspText { get; }
    public string DateText { get; }

    [ObservableProperty] private bool _isChecked;
    private bool _refreshing;

    public VersionRowViewModel(ModVersion version, ModListItemViewModel row, bool fits, bool isNewestFit,
        bool isInstalled)
    {
        _row = row;
        Version = version;
        Fits = fits;
        IsNewestFit = isNewestFit;
        IsInstalled = isInstalled;
        KspText = $"KSP {CompatibilityService.DescribeKspRangeExact(version)}";
        DateText = version.ReleaseDate?.ToString("yyyy-MM-dd") ?? "";
        Refresh();
    }

    public void Refresh()
    {
        _refreshing = true;
        IsChecked = _row.IsSelected && _row.SelectedVersion == Version;
        _refreshing = false;
    }

    partial void OnIsCheckedChanged(bool value)
    {
        if (_refreshing) return;

        if (value) _row.SelectVersion(Version);
        else _row.IsSelected = false;
    }
}

// A depends entry with whether the "Check against" instance has it
public record DependsRowViewModel(string Name, bool IsInstalled, bool ShowState)
{
    public string StateText => !ShowState ? "" : IsInstalled ? "installed" : "not installed";

    public static DependsRowViewModel For(Relationship relationship, IReadOnlyList<ModVersion> installed,
        bool showState)
    {
        var names = relationship.AnyOf?.Select(e => e.Name).OfType<string>().ToList() ??
                    (relationship.Name is null ? [] : [relationship.Name]);
        var isInstalled = installed.Any(m =>
            names.Contains(m.Identifier) || (m.Provides?.Any(names.Contains) ?? false));

        return new DependsRowViewModel(string.Join(" or ", names), isInstalled, showState);
    }
}

public partial class LinkViewModel(string label, string url)
{
    public string Label { get; } = label;
    public string Url { get; } = url;

    [RelayCommand]
    private void Open()
    {
        try
        {
            Process.Start(new ProcessStartInfo(Url) { UseShellExecute = true });
        }
        catch (Exception)
        {
            // No browser to open it with; nothing else to do
        }
    }
}
