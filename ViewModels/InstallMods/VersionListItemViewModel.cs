using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using KMRLauncherMvvm.Models;

namespace KMRLauncherMvvm.ViewModels.InstallMods;

public partial class VersionListItemViewModel : ObservableObject
{
    public ModVersion Version { get; }

    public HashSet<string> Sources { get; } = [];

    [ObservableProperty] private bool _isSelected;

    public VersionListItemViewModel(ModVersion version, string source, bool isSelected)
    {
        Version = version;
        Sources.Add(source);
        IsSelected = isSelected;
    }
}