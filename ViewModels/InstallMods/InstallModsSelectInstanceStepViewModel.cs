using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using KMRLauncherMvvm.Models;

namespace KMRLauncherMvvm.ViewModels.InstallMods;

public partial class InstallModsSelectInstanceStepViewModel : InstallModsStepViewModel
{
    [ObservableProperty] private ObservableCollection<Instance> _instances;
    private ModListService _modListService;

    public InstallModsSelectInstanceStepViewModel(InstallModsData installModsData, ModListService modListService) :
        base(installModsData)
    {
        InstallModsData.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Models.InstallModsData.SelectedInstance))
            {
                ResolveDependencies();
                OnPropertyChanged(nameof(CanGoNext));
            }
        };

        _modListService = modListService;
        _instances = App.Settings.Instances;

        InstallModsData.SelectedInstance = Instances.FirstOrDefault();
        InstallModsData.RequestedModVersions = installModsData.RequestedModVersions;
    }

    private void ResolveDependencies()
    {
        var tempList = InstallModsData.RequestedModVersions.ToList();
        tempList.RemoveAll(v => v.ModsForReason.Count > 0);
        InstallModsData.RequestedModVersions = new ObservableCollection<ModVersion>(tempList);
        if (_modListService.Mods is null || InstallModsData.SelectedInstance is null) return;

        var tempModVersions = InstallModsData.RequestedModVersions;

        foreach (var version in tempModVersions)
        {
            if (InstallModsData.SelectedInstance.Mods.Exists(m => m.Id == version.Identifier))
            {
                InstallModsData.RequestedModVersions.Remove(version);
            }
        }

        var i = 0;
        while (i < InstallModsData.RequestedModVersions.Count)
        {
            var version = InstallModsData.RequestedModVersions[i];

            if (version.Depends is null || version.Depends.Count == 0)
            {
                i++;
                continue;
            }

            foreach (var dependency in version.Depends)
            {
                if (!_modListService.Mods.ToList().Exists(m => m.Id == dependency.Name)) continue;

                if (dependency.MinVersion is not null)
                {
                    continue;
                }

                if (dependency.MaxVersion is not null)
                {
                    continue;
                }

                if (dependency.AnyOf is not null)
                {
                    continue;
                }

                if (dependency.Version is not null)
                {
                    continue;
                }
                
                if (InstallModsData.SelectedInstance.Mods.Exists(m => m.Identifier == dependency.Name))
                    continue;

                if (InstallModsData.RequestedModVersions.ToList().Exists(v => v.Identifier == dependency.Name))
                {
                    InstallModsData.RequestedModVersions.First(v => v.Identifier == dependency.Name).ModsForReason
                        .Add(version.Id);
                    continue;
                }

                var versionToAdd = _modListService.Mods.First(m => m.Id == dependency.Name)
                    .Versions.First();
                versionToAdd.ModsForReason = [version.Id];
                InstallModsData.RequestedModVersions.Add(versionToAdd);
            }

            i++;
        }
    }

    public override string Title => "Choose instance";
    public override bool CanGoNext => InstallModsData.SelectedInstance is not null;
}