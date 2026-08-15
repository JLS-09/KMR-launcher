using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using KMRLauncherMvvm.Models;
using KMRLauncherMvvm.Services;

namespace KMRLauncherMvvm.ViewModels.InstallMods;

public partial class InstallModsSelectInstanceStepViewModel : InstallModsStepViewModel
{
    [ObservableProperty] private ObservableCollection<Instance> _instances;
    private ModListService _modListService;
    private CompatibilityService _compatibilityService;

    public InstallModsSelectInstanceStepViewModel(InstallModsData installModsData, ModListService modListService, CompatibilityService compatibilityService) :
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
        _compatibilityService = compatibilityService;
        _instances = App.Settings.Instances;

        InstallModsData.SelectedInstance = Instances.FirstOrDefault();
        InstallModsData.RequestedModVersions = installModsData.RequestedModVersions;
    }

    private void ResolveDependencies()
    {
        InstallModsData.ChoosableVersions.Clear();
        var tempList = InstallModsData.RequestedModVersions.ToList();
        tempList.RemoveAll(v => v.ModsForReason.Count > 0);
        InstallModsData.RequestedModVersions = new ObservableCollection<ModVersion>(tempList);
        if (_modListService.Mods is null || InstallModsData.SelectedInstance is null) return;

        var tempModVersions = InstallModsData.RequestedModVersions.ToList();

        foreach (var version in tempModVersions.Where(version => InstallModsData.SelectedInstance.Mods.Exists(m => m.Id == version.Identifier)))
        {
            InstallModsData.RequestedModVersions.Remove(version);
        }

        var i = 0;
        while (i < InstallModsData.RequestedModVersions.Count)
        {
            var version = InstallModsData.RequestedModVersions[i];

            if (version.Id is null)
            {
                return;
            }

            if (version.Depends is null || version.Depends.Count == 0)
            {
                i++;
                continue;
            }

            foreach (var dependency in version.Depends)
            {
                if (dependency.AnyOf is not null || !_modListService.Mods.ToList().Exists(m => m.Id == dependency.Name))
                {
                    InstallModsData.ChoosableVersions.Add(dependency);
                    continue;
                }
                
                if (dependency.Version is not null &&
                    (dependency.MinVersion is not null || dependency.MaxVersion is not null)) continue;

                if (InstallModsData.SelectedInstance.Mods.Exists(v => v.Identifier == dependency.Name))
                {
                    var existingVersion = InstallModsData.SelectedInstance.Mods.First(v => v.Identifier == dependency.Name);

                    if (_compatibilityService.IsVersionCompatibleWithRelation(existingVersion, dependency))
                    {
                        existingVersion.ModsForReason.Add(version.Id);
                        continue;
                    }
                    
                    InstallModsData.RequestedRemoveModVersions.Add(existingVersion);
                    var versionToAdd = _compatibilityService.GetCompatibleVersionFromRelation(dependency);
                    versionToAdd.ModsForReason = [..existingVersion.ModsForReason, version.Id];
                    InstallModsData.RequestedModVersions.Add(versionToAdd);
                    continue;
                }

                if (InstallModsData.RequestedModVersions.ToList()
                    .Exists(v => v.Identifier == dependency.Name))
                {
                    var existingVersion = InstallModsData.RequestedModVersions.ToList().First(v => v.Identifier == dependency.Name);
                    
                    if (_compatibilityService.IsVersionCompatibleWithRelation(existingVersion, dependency))
                    {
                        existingVersion.ModsForReason.Add(version.Id);
                        continue;
                    }
                    
                    InstallModsData.RequestedModVersions.Remove(existingVersion);
                    var versionToAdd = _compatibilityService.GetCompatibleVersionFromRelation(dependency);
                    versionToAdd.ModsForReason = [..existingVersion.ModsForReason, version.Id];
                    InstallModsData.RequestedModVersions.Add(versionToAdd);
                    continue;
                }
                
                var compatibleVersion = _compatibilityService.GetCompatibleVersionFromRelation(dependency);
                compatibleVersion.ModsForReason = [version.Id];
                InstallModsData.RequestedModVersions.Add(compatibleVersion);
            }

            i++;
        }
    }

    public override string Title => "Choose instance";
    public override bool CanGoNext => InstallModsData.SelectedInstance is not null;
    public override void PopulateRecommendations()
    {
        throw new System.NotImplementedException();
    }
}