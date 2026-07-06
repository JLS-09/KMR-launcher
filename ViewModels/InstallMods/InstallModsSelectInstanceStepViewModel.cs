using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
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
                if (!_modListService.Mods.ToList().Exists(m => m.Id == dependency.Name)) continue;

                var dependencyMod = _modListService.Mods.First(m => m.Id == dependency.Name);

                if (dependency.AnyOf is null && dependency.MaxVersion is null && dependency.MinVersion is null &&
                    dependency.Version is null)
                {
                    if (InstallModsData.SelectedInstance.Mods.Exists(v => v.Identifier == dependency.Name))
                        continue;

                    if (InstallModsData.RequestedModVersions.ToList().Exists(v => v.Identifier == dependency.Name))
                    {
                        InstallModsData.RequestedModVersions.First(v => v.Identifier == dependency.Name).ModsForReason
                            .Add(version.Id);
                        continue;
                    }

                    var versionToAdd = dependencyMod.Versions.First();
                    versionToAdd.ModsForReason = [version.Id];
                    InstallModsData.RequestedModVersions.Add(versionToAdd);
                    
                    continue;
                }

                // TODO implement MinVersion + MaxVersion
                if (dependency.MinVersion is not null)
                {
                    var minDepVersion =
                        dependencyMod.Versions.FirstOrDefault(v =>
                            v.Id == $"{dependency.Name}-{dependency.MinVersion}");

                    var minDepVersionIndex = 0;

                    if (minDepVersion is not null)
                        minDepVersionIndex = dependencyMod.Versions.IndexOf(minDepVersion);

                    if (InstallModsData.SelectedInstance.Mods.Exists(v => v.Identifier == dependency.Name))
                    {
                        var versionInInstance =
                            InstallModsData.SelectedInstance.Mods.First(v => v.Identifier == dependency.Name);

                        var installedVersionIndex = dependencyMod.Versions.IndexOf(versionInInstance);

                        if (installedVersionIndex <= minDepVersionIndex) continue;

                        InstallModsData.RequestedRemoveModVersions.Add(versionInInstance);

                        var versionToAdd = dependencyMod.Versions.First();
                        versionToAdd.ModsForReason = versionInInstance.ModsForReason;
                        versionToAdd.ModsForReason.Add(version.Id);
                        InstallModsData.RequestedModVersions.Add(versionToAdd);
                        
                        continue;
                    }
                    
                    if (InstallModsData.RequestedModVersions.ToList()
                             .Exists(v => v.Identifier == dependency.Name))
                    {
                        var addedDepVersionIndex = dependencyMod.Versions.IndexOf(
                            InstallModsData.RequestedModVersions.First(v => v.Identifier == dependency.Name));

                        if (addedDepVersionIndex <= minDepVersionIndex)
                        {
                            InstallModsData.RequestedModVersions.First(v => v.Identifier == dependency.Name)
                                .ModsForReason
                                .Add(version.Id);
                            continue;
                        }

                        var versionToAdd = dependencyMod.Versions.First();
                        versionToAdd.ModsForReason = dependencyMod.Versions[addedDepVersionIndex].ModsForReason;
                        versionToAdd.ModsForReason.Add(version.Id);
                        InstallModsData.RequestedModVersions.Add(versionToAdd);
                        
                        InstallModsData.RequestedModVersions.Remove(dependencyMod.Versions[addedDepVersionIndex]);
                    }
                    continue;
                }

                if (dependency.MaxVersion is not null)
                {
                    var depVersionIndex = dependencyMod.Versions.IndexOf(
                        dependencyMod.Versions.First(v => v.Id == $"{dependency.Name}-{dependency.MaxVersion}"));

                    if (InstallModsData.SelectedInstance.Mods.Exists(v => v.Identifier == dependency.Name))
                    {
                        var installedVersionIndex = dependencyMod.Versions.IndexOf(
                            InstallModsData.SelectedInstance.Mods.First(v => v.Identifier == dependency.Name));
                        if (installedVersionIndex >= depVersionIndex)
                            continue;

                        InstallModsData.RequestedModVersions.Add(dependencyMod.Versions.First());
                    }

                    InstallModsData.RequestedModVersions.Add(dependencyMod.Versions
                        .First(v => v.Id == $"{dependency.Name}-{dependency.MaxVersion}"));

                    continue;
                }

                // TODO implement anyOf
                if (dependency.AnyOf is not null)
                {
                    continue;
                }

                if (dependency.Version is not null)
                {
                    if (InstallModsData.SelectedInstance.Mods.Exists(v =>
                            v.Id == $"{dependency.Name}-{dependency.Version}"))
                        continue;
                }
            }

            i++;
        }
    }

    public override string Title => "Choose instance";
    public override bool CanGoNext => InstallModsData.SelectedInstance is not null;
}