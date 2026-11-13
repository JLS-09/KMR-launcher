using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using KMRLauncherMvvm.Models;
using KMRLauncherMvvm.Services;

namespace KMRLauncherMvvm.ViewModels;

public partial class CompatibleKspVersionsViewModel : ViewModelBase
{
    private readonly Instance _instance;
    private readonly List<string> _unknownEntries;

    public string Title { get; }
    public string InstanceVersionText { get; }
    public ObservableCollection<KspVersionGroupViewModel> Groups { get; }

    public CompatibleKspVersionsViewModel(Instance instance)
    {
        _instance = instance;

        var instanceVersion = KspVersions.ToFullVersion(instance.Version);
        var saved = instance.CompatibleKspVersions.ToHashSet();

        Title = $"COMPATIBLE KSP VERSIONS — {instance.Name.ToUpper()}";
        InstanceVersionText =
            $"Mods for KSP {instanceVersion} (this instance) are always allowed. Tick other versions to allow " +
            $"mods made for them too. \"All x.y.x\" doesn't count for mods that are marked as strict; " +
            $"tick their exact version instead.";

        Groups = new ObservableCollection<KspVersionGroupViewModel>(
            KspVersions.All
                .Select(KspVersions.ToFullVersion)
                .Distinct()
                .GroupBy(KspVersions.ToMinorPrefix)
                .Select(group => new KspVersionGroupViewModel(
                    group.Key,
                    saved.Contains(group.Key),
                    group.Select(v => new KspVersionOptionViewModel(v, v == instanceVersion, saved.Contains(v)))
                        .ToList())));

        var shown = Groups.SelectMany(g => g.Versions.Select(v => v.Version).Prepend(g.Prefix)).ToHashSet();
        _unknownEntries = saved.Where(entry => !shown.Contains(entry)).ToList();
    }

    public CompatibleKspVersionsViewModel() : this(new Instance("Design", "", "1.12.5"))
    {
    }

    [RelayCommand]
    private void Cancel(Window window) => window.Close();

    [RelayCommand]
    private void Save(Window window)
    {
        _instance.CompatibleKspVersions = [.. Groups.SelectMany(g => g.ToEntries()), .. _unknownEntries];
        SettingsService.Save(App.Settings);
        window.Close();
    }
}