using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using KMRLauncherMvvm.Models;

namespace KMRLauncherMvvm.ViewModels.InstallMods;

public partial class ChoiceOptionViewModel : ObservableObject
{
    private readonly PlannedModVersion _option;

    public string Identifier { get; }
    public List<ModVersion> Versions { get; }
    public bool HasMultipleVersions { get; }

    private ModVersion _selectedVersion;

    public ModVersion? SelectedVersion
    {
        get => _selectedVersion;
        set
        {
            if (value is null) return;
            SetProperty(ref _selectedVersion, value);
        }
    }

    public ChoiceOptionViewModel(PlannedModVersion option, List<ModVersion> versions)
    {
        _option = option;
        Identifier = option.Version.Identifier;

        Versions = versions;
        if (!Versions.Contains(option.Version))
            Versions.Insert(0, option.Version);

        HasMultipleVersions = Versions.Count > 1;
        _selectedVersion = option.Version;
    }

    public PlannedModVersion ToPlanned() => new()
    {
        Version = _selectedVersion,
        Relationship = _option.Relationship,
        RequiredBy = [.. _option.RequiredBy]
    };
}