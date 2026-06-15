using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace KMRLauncherMvvm.Models;

public partial class InstallModsData : ObservableObject
{
    [ObservableProperty] private Instance? _selectedInstance;
    [ObservableProperty] private ObservableCollection<ModVersion> _requestedModVersions;
    [ObservableProperty] private ObservableCollection<ModVersion> _requestedRemoveModVersions;
    [ObservableProperty] private ObservableCollection<Mod> extraMods;
}