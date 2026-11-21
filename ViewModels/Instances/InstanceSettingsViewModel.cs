using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KMRLauncherMvvm.Models;
using KMRLauncherMvvm.Services;

namespace KMRLauncherMvvm.ViewModels.Instances;

public partial class InstanceSettingsViewModel : ObservableObject
{
    private const int NewestGroupsShown = 5;

    private readonly List<KspVersionGroupViewModel> _allGroups;
    private readonly HashSet<KspVersionGroupViewModel> _alwaysShown;
    private readonly List<string> _unknownEntries;
    private readonly string _instanceVersion;
    private Snapshot _saved;

    public string RootPath { get; }
    public string FolderText { get; }
    public string DefaultPrefix { get; }
    public bool HasOlderGroups { get; }
    public string OlderRangeText { get; }

    [ObservableProperty] private string _name;
    [ObservableProperty] private bool _isPrimary;
    [ObservableProperty] private string _prefix;
    [ObservableProperty] private string _launchArguments;
    [ObservableProperty] private bool _showOlder;
    [ObservableProperty] private IReadOnlyList<KspVersionGroupViewModel> _groups = [];
    [ObservableProperty] private int _changeCount;
    [ObservableProperty] private string _changesText = "";
    [ObservableProperty] private string? _nameError;

    [ObservableProperty] [NotifyCanExecuteChangedFor(nameof(SaveCommand), nameof(RevertCommand))]
    private bool _hasChanges;

    public InstanceSettingsViewModel(Instance instance, IEnumerable<Instance> allInstances)
    {
        RootPath = instance.RootPath;
        FolderText = InstanceTexts.ShortPath(instance.RootPath);
        DefaultPrefix = InstanceTexts.ShortPath(WinePrefixes.DefaultFor(instance, allInstances));
        _instanceVersion = KspVersions.ToFullVersion(instance.Version);

        _saved = new Snapshot(instance.Name, instance.IsPrimary, Expanded(instance.PrefixPath),
            instance.LaunchArguments, [.. instance.CompatibleKspVersions]);
        _name = instance.Name;
        _isPrimary = instance.IsPrimary;
        _prefix = InstanceTexts.ShortPath(instance.PrefixPath);
        _launchArguments = instance.LaunchArguments;

        _allGroups =
        [
            .. KspVersions.All
                .Select(KspVersions.ToFullVersion)
                .Distinct()
                .GroupBy(KspVersions.ToMinorPrefix)
                .Select(group => new KspVersionGroupViewModel(
                    group.Key,
                    _saved.Compatible.Contains(group.Key),
                    [
                        .. group.Reverse().Select(v => new KspVersionOptionViewModel(v, v == _instanceVersion,
                            _saved.Compatible.Contains(v)))
                    ]))
        ];

        var listed = _allGroups.SelectMany(g => g.Versions.Select(v => v.Version).Prepend(g.Prefix)).ToHashSet();
        _unknownEntries = [.. _saved.Compatible.Where(entry => !listed.Contains(entry))];

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
        Recount();
    }

    public string OlderToggleText => $"{(ShowOlder ? "Hide" : "Show")} {OlderRangeText}";

    partial void OnShowOlderChanged(bool value)
    {
        UpdateGroups();
        OnPropertyChanged(nameof(OlderToggleText));
    }

    [RelayCommand]
    private void ToggleOlder() => ShowOlder = !ShowOlder;

    private void UpdateGroups() =>
        Groups = ShowOlder ? _allGroups : [.. _allGroups.Where(_alwaysShown.Contains)];

    partial void OnNameChanged(string value) => Recount();
    partial void OnIsPrimaryChanged(bool value) => Recount();
    partial void OnPrefixChanged(string value) => Recount();
    partial void OnLaunchArgumentsChanged(string value) => Recount();

    private void OnVersionChanged(object? sender, PropertyChangedEventArgs e) => Recount();

    private List<string> CompatibleEntries() => [.. _allGroups.SelectMany(g => g.ToEntries()), .. _unknownEntries];

    private string PrefixToSave => Prefix.Trim().Length == 0 ? Expanded(DefaultPrefix) : Expanded(Prefix);

    private void Recount()
    {
        var changes = 0;
        if (Name.Trim() != _saved.Name) changes++;
        if (IsPrimary != _saved.IsPrimary) changes++;
        if (PrefixToSave != _saved.Prefix) changes++;
        if (LaunchArguments.Trim() != _saved.LaunchArguments.Trim()) changes++;

        var compatible = CompatibleEntries().ToHashSet();
        changes += compatible.Count(e => !_saved.Compatible.Contains(e)) +
                   _saved.Compatible.Count(e => !compatible.Contains(e));

        ChangeCount = changes;
        HasChanges = changes > 0;
        ChangesText = changes == 1 ? "1 unsaved change" : $"{changes} unsaved changes";
        NameError = Name.Trim().Length == 0 ? "The instance needs a name." : null;
        SaveCommand.NotifyCanExecuteChanged();
    }

    private bool CanSave() => HasChanges && NameError is null;

    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save()
    {
        var instance = App.Settings.Instances.FirstOrDefault(i => i.RootPath == RootPath);
        if (Apply(App.Settings)) SettingsService.Save(App.Settings, instance);
    }

    public bool Apply(AppSettings settings)
    {
        var instance = settings.Instances.FirstOrDefault(i => i.RootPath == RootPath);
        if (instance is null || !CanSave()) return false;

        instance.Name = Name.Trim();
        if (IsPrimary)
            foreach (var other in settings.Instances)
                other.IsPrimary = other == instance;
        else
            instance.IsPrimary = false;
        instance.PrefixPath = PrefixToSave;
        instance.LaunchArguments = LaunchArguments.Trim();
        instance.CompatibleKspVersions = CompatibleEntries();

        _saved = new Snapshot(instance.Name, instance.IsPrimary, instance.PrefixPath, instance.LaunchArguments,
            [.. instance.CompatibleKspVersions]);
        Name = instance.Name;
        Prefix = InstanceTexts.ShortPath(instance.PrefixPath);
        LaunchArguments = instance.LaunchArguments;
        Recount();
        return true;
    }

    [RelayCommand(CanExecute = nameof(HasChanges))]
    private void Revert()
    {
        Name = _saved.Name;
        IsPrimary = _saved.IsPrimary;
        Prefix = InstanceTexts.ShortPath(_saved.Prefix);
        LaunchArguments = _saved.LaunchArguments;

        foreach (var group in _allGroups)
        {
            group.AllowAll = _saved.Compatible.Contains(group.Prefix);
            foreach (var option in group.Versions.Where(v => v.CanToggle))
                option.IsChecked = _saved.Compatible.Contains(option.Version);
        }
    }

    private static string Expanded(string path) => path.Trim().Length == 0 ? "" : WinePrefixes.Expand(path);

    private static string RangeText(string lowest, string highest)
    {
        var lowMajor = lowest.Split('.')[0];
        return lowMajor == highest.Split('.')[0] ? $"{lowest} – {highest}" : $"{lowMajor}.x – {highest}";
    }

    private sealed record Snapshot(
        string Name,
        bool IsPrimary,
        string Prefix,
        string LaunchArguments,
        HashSet<string> Compatible);
}