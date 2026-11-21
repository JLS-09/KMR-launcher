using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KMRLauncherMvvm.Models;

namespace KMRLauncherMvvm.ViewModels;

public partial class CompatibleVersionsViewModel : ObservableObject
{
    private const int NewestGroupsShown = 5;

    private readonly List<KspVersionGroupViewModel> _allGroups;
    private readonly HashSet<KspVersionGroupViewModel> _alwaysShown;
    private readonly List<string> _unknownEntries;

    public bool HasOlderGroups { get; }
    public string OlderRangeText { get; }

    [ObservableProperty] private bool _showOlder;
    [ObservableProperty] private IReadOnlyList<KspVersionGroupViewModel> _groups = [];

    public event Action? Changed;

    public CompatibleVersionsViewModel(string instanceVersion, IReadOnlyCollection<string> compatible)
    {
        var fullVersion = KspVersions.ToFullVersion(instanceVersion);
        var entries = compatible.ToHashSet();

        _allGroups =
        [
            .. KspVersions.All
                .Select(KspVersions.ToFullVersion)
                .Distinct()
                .GroupBy(KspVersions.ToMinorPrefix)
                .Select(group => new KspVersionGroupViewModel(
                    group.Key,
                    entries.Contains(group.Key),
                    [
                        .. group.Reverse().Select(v => new KspVersionOptionViewModel(v, v == fullVersion,
                            entries.Contains(v)))
                    ]))
        ];

        var listed = _allGroups.SelectMany(g => g.Versions.Select(v => v.Version).Prepend(g.Prefix)).ToHashSet();
        _unknownEntries = [.. compatible.Where(entry => !listed.Contains(entry))];

        _alwaysShown =
        [
            .. _allGroups.Take(NewestGroupsShown),
            .. _allGroups.Where(g => g.AllowAll || g.Versions.Any(v => v.IsChecked))
        ];
        var older = _allGroups.Where(g => !_alwaysShown.Contains(g)).ToList();
        HasOlderGroups = older.Count > 0;
        OlderRangeText = HasOlderGroups ? RangeText(older[^1].Prefix, older[0].Prefix) : "";

        foreach (var group in _allGroups)
        {
            group.PropertyChanged += OnVersionChanged;
            foreach (var option in group.Versions) option.PropertyChanged += OnVersionChanged;
        }

        UpdateGroups();
    }

    public string OlderToggleText => $"{(ShowOlder ? "Hide" : "Show")} {OlderRangeText}";

    public List<string> Entries() => [.. _allGroups.SelectMany(g => g.ToEntries()), .. _unknownEntries];

    public void Reset(IReadOnlyCollection<string> compatible)
    {
        foreach (var group in _allGroups)
        {
            group.AllowAll = compatible.Contains(group.Prefix);
            foreach (var option in group.Versions.Where(v => v.CanToggle))
                option.IsChecked = compatible.Contains(option.Version);
        }
    }

    partial void OnShowOlderChanged(bool value)
    {
        UpdateGroups();
        OnPropertyChanged(nameof(OlderToggleText));
    }

    [RelayCommand]
    private void ToggleOlder() => ShowOlder = !ShowOlder;

    private void UpdateGroups() =>
        Groups = ShowOlder ? _allGroups : [.. _allGroups.Where(_alwaysShown.Contains)];

    private void OnVersionChanged(object? sender, PropertyChangedEventArgs e) => Changed?.Invoke();

    private static string RangeText(string lowest, string highest)
    {
        var lowMajor = lowest.Split('.')[0];
        return lowMajor == highest.Split('.')[0] ? $"{lowest} – {highest}" : $"{lowMajor}.x – {highest}";
    }
}
