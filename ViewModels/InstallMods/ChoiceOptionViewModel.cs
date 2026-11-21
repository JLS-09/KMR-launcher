using System;
using System.Collections.Generic;
using System.Linq;
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
            if (!SetProperty(ref _selectedVersion, value)) return;
            OnPropertyChanged(nameof(DescriptionText));
            OnPropertyChanged(nameof(IsCached));
        }
    }

    [ObservableProperty] private bool _isSelected;

    private readonly Func<ModVersion, bool>? _isCached;
    private readonly Func<string, string?>? _abstractOf;

    public string DescriptionText => string.Join(" · ", new[]
    {
        _abstractOf?.Invoke(_selectedVersion.Identifier),
        _selectedVersion.DownloadSize is { } size ? Services.InstalledMods.FormatSize(size) : null
    }.Where(p => !string.IsNullOrWhiteSpace(p)));

    public bool IsCached => _isCached?.Invoke(_selectedVersion) == true;

    public ChoiceOptionViewModel(PlannedModVersion option, List<ModVersion> versions,
        Func<ModVersion, bool>? isCached = null, Func<string, string?>? abstractOf = null)
    {
        _isCached = isCached;
        _abstractOf = abstractOf;
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
        IsVersionPickedByUser = _selectedVersion != _option.Version,
        Relationship = _option.Relationship,
        RequiredBy = [.. _option.RequiredBy]
    };
}