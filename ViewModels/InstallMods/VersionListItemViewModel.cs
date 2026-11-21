using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using KMRLauncherMvvm.Models;

namespace KMRLauncherMvvm.ViewModels.InstallMods;

public partial class VersionListItemViewModel : ObservableObject
{
    public string Identifier { get; }
    public List<ModVersion> Versions { get; }
    public bool HasMultipleVersions { get; }

    public HashSet<string> Sources { get; }
    public string SourcesText { get; }

    [ObservableProperty] private bool _isSelected;

    [ObservableProperty] private string _extraDependenciesText = "";
    private readonly Func<ModVersion, int>? _countMissingDepends;

    private ModVersion _selectedVersion;

    public ModVersion? SelectedVersion
    {
        get => _selectedVersion;
        set
        {
            if (value is null || !SetProperty(ref _selectedVersion, value)) return;

            IsSelected = true;
            UpdateExtraDependencies();
        }
    }

    public VersionListItemViewModel(string identifier, List<ModVersion> versions, IEnumerable<string> sources,
        string sourcesPrefix, bool isSelected, string? preferredVersionId = null,
        Func<ModVersion, int>? countMissingDepends = null)
    {
        _countMissingDepends = countMissingDepends;
        Identifier = identifier;
        Versions = versions;
        HasMultipleVersions = versions.Count > 1;
        Sources = [.. sources];
        SourcesText = $"{sourcesPrefix} {JoinAll([.. Sources])}";

        _selectedVersion = versions.FirstOrDefault(v => v.Id == preferredVersionId) ?? versions[0];
        _isSelected = isSelected;
        UpdateExtraDependencies();
    }

    private void UpdateExtraDependencies()
    {
        var missing = _countMissingDepends?.Invoke(_selectedVersion) ?? 0;
        ExtraDependenciesText = missing switch
        {
            0 => "",
            1 => "+1 dependency",
            _ => $"+{missing} dependencies"
        };
    }

    private static string JoinAll(IReadOnlyList<string> names) => names.Count <= 1
        ? string.Join("", names)
        : $"{string.Join(", ", names.Take(names.Count - 1))} and {names[^1]}";
}