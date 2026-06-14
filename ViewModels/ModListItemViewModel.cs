using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using KMRLauncherMvvm.Models;

namespace KMRLauncherMvvm.ViewModels;

public partial class ModListItemViewModel : ObservableObject
{
    public Mod Mod { get; }

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private ModVersion _selectedVersion;

    public ModListItemViewModel(Mod mod)
    {
        Mod = mod;
        _selectedVersion = mod.Versions[0];
    }
}