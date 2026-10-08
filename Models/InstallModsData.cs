using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace KMRLauncherMvvm.Models;

public partial class InstallModsData : ObservableObject
{
    [ObservableProperty] private Instance? _selectedInstance;
    [ObservableProperty] private ObservableCollection<ModVersionDurum> _requestedModVersions = [];
    [ObservableProperty] private ObservableCollection<ModVersionDurum> _finalModList = [];
    [ObservableProperty] private ObservableCollection<ModVersionDurum> _requestedRemoveModVersions = [];
    [ObservableProperty] private HashSet<Relationship> _choosableVersions = [];
    [ObservableProperty] private ObservableCollection<string> _modsWithIgnoredRecommendations = [];
}