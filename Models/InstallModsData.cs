using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace KMRLauncherMvvm.Models;

public partial class InstallModsData : ObservableObject
{
    [ObservableProperty] private Instance? _selectedInstance;
    [ObservableProperty] private ObservableCollection<Mod> _requestedMods;
    [ObservableProperty] private ModVersion? _requestedVersion;
    [ObservableProperty] private ObservableCollection<ModVersion> _availableVersions = [];
    [ObservableProperty] private ObservableCollection<Mod> extraMods;
}