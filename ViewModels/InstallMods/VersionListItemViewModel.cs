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

    private ModVersion _selectedVersion;

    public ModVersion? SelectedVersion
    {
        get => _selectedVersion;
        set
        {
            if (value is null || !SetProperty(ref _selectedVersion, value)) return;

            IsSelected = true;
        }
    }

    public VersionListItemViewModel(string identifier, List<ModVersion> versions, IEnumerable<string> sources,
        string sourcesPrefix, bool isSelected, string? preferredVersionId = null)
    {
        Identifier = identifier;
        Versions = versions;
        HasMultipleVersions = versions.Count > 1;
        Sources = [.. sources];
        SourcesText = $"{sourcesPrefix} {string.Join(", ", Sources)}";

        _selectedVersion = versions.FirstOrDefault(v => v.Id == preferredVersionId) ?? versions[0];
        _isSelected = isSelected;
    }
}