using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace KMRLauncherMvvm.Models;

public partial class InstallModsData : ObservableObject
{
    [ObservableProperty] private Instance? _selectedInstance;
    [ObservableProperty] private ObservableCollection<Mod> _requestedMods;
    [ObservableProperty] private ObservableCollection<ModVersion> _requestedVersions;
    [ObservableProperty] private ObservableCollection<Mod> extraMods;
}