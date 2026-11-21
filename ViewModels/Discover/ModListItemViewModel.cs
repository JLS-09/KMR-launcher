using System;
using CommunityToolkit.Mvvm.ComponentModel;
using KMRLauncherMvvm.Controls;
using KMRLauncherMvvm.Models;

namespace KMRLauncherMvvm.ViewModels.Discover;

public partial class ModListItemViewModel : ObservableObject
{
    public Mod Mod { get; }
    public string Authors { get; }
    [ObservableProperty] private string _downloads = "–";

    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private ModVersion _selectedVersion;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatus))]
    [NotifyPropertyChangedFor(nameof(StatusKind))]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private ModFit _fit = ModFit.None;

    public bool HasStatus => Fit.Kind is not null;
    public ModStatusKind StatusKind => Fit.Kind ?? ModStatusKind.Fits;
    public string StatusText => Fit.Text;

    private bool _versionPicked;

    public event Action<ModListItemViewModel>? SelectionChanged;

    public ModListItemViewModel(Mod mod)
    {
        Mod = mod;
        Authors = DiscoverFilter.ShortAuthors(mod.Author);
        _selectedVersion = mod.Versions[0];
    }

    public void SelectVersion(ModVersion version)
    {
        _versionPicked = true;
        SelectedVersion = version;
        IsSelected = true;
    }

    partial void OnIsSelectedChanged(bool value)
    {
        if (value && !_versionPicked) SelectedVersion = Fit.NewestFit ?? Mod.Versions[0];
        if (!value) _versionPicked = false;
        SelectionChanged?.Invoke(this);
    }

    partial void OnFitChanged(ModFit value)
    {
        if (!IsSelected) SelectedVersion = value.NewestFit ?? Mod.Versions[0];
    }
}