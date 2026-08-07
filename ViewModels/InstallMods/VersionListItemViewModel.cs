using CommunityToolkit.Mvvm.ComponentModel;
using KMRLauncherMvvm.Models;

namespace KMRLauncherMvvm.ViewModels.InstallMods;

public partial class VersionListItemViewModel : ObservableObject
{
    public ModVersion Version { get; }

    [ObservableProperty] private bool _isSelected;

    public VersionListItemViewModel(ModVersion version)
    {
        Version = version;
    }
}