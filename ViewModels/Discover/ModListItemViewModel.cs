using System;
using CommunityToolkit.Mvvm.ComponentModel;
using KMRLauncherMvvm.Controls;
using KMRLauncherMvvm.Models;

namespace KMRLauncherMvvm.ViewModels.Discover;

// One row in Discover's mod list
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

    // True once the user picked a version in the details panel; then selecting keeps that version
    private bool _versionPicked;

    // Raised when IsSelected changes, so the page can keep its list of selected mods
    public event Action<ModListItemViewModel>? SelectionChanged;

    public ModListItemViewModel(Mod mod)
    {
        Mod = mod;
        Authors = DiscoverFilter.ShortAuthors(mod.Author);
        _selectedVersion = mod.Versions[0];
    }

    // Selects the mod with this version (from the details panel's version list)
    public void SelectVersion(ModVersion version)
    {
        _versionPicked = true;
        SelectedVersion = version;
        IsSelected = true;
    }

    partial void OnIsSelectedChanged(bool value)
    {
        // Ticking the row picks the newest version that fits the "Check against" instance
        if (value && !_versionPicked) SelectedVersion = Fit.NewestFit ?? Mod.Versions[0];
        if (!value) _versionPicked = false;
        SelectionChanged?.Invoke(this);
    }

    partial void OnFitChanged(ModFit value)
    {
        if (!IsSelected) SelectedVersion = value.NewestFit ?? Mod.Versions[0];
    }
}
