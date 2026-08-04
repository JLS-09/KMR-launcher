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

    //TODO refactor this method
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
                if (!_modListService.Mods.ToList().Exists(m => m.Id == dependency.Name) || dependency.AnyOf is not null)
                {
                    InstallModsData.ChoosableVersions.Add(dependency);
                    continue;
                }

                var dependencyMod = _modListService.Mods.First(m => m.Id == dependency.Name);

                if (dependency.Version is not null &&
                    (dependency.MinVersion is not null || dependency.MaxVersion is not null)) continue;

                if (dependency.MaxVersion is null && dependency.MinVersion is null &&
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

                if (dependency.MinVersion is not null && dependency.MaxVersion is not null)
                {
                    var minDepVersion =
                        dependencyMod.Versions.FirstOrDefault(v =>
                            v.Id == $"{dependency.Name}-{dependency.MinVersion}");

                    var maxDepVersion =
                        dependencyMod.Versions.FirstOrDefault(v =>
                            v.Id == $"{dependency.Name}-{dependency.MaxVersion}");

                    int maxDepVersionIndex;
                    var minDepVersionIndex = maxDepVersionIndex = 0;

                    if (minDepVersion is not null)
                        minDepVersionIndex = dependencyMod.Versions.IndexOf(minDepVersion);

                    if (maxDepVersion is not null)
                        maxDepVersionIndex = dependencyMod.Versions.IndexOf(maxDepVersion);

                    if (InstallModsData.SelectedInstance.Mods.Exists(v => v.Identifier == dependency.Name))
                    {
                        var versionInInstance =
                            InstallModsData.SelectedInstance.Mods.First(v => v.Identifier == dependency.Name);

                        var installedVersionIndex = dependencyMod.Versions.IndexOf(versionInInstance);

                        if (installedVersionIndex <= minDepVersionIndex &&
                            installedVersionIndex >= maxDepVersionIndex) continue;

                        InstallModsData.RequestedRemoveModVersions.Add(versionInInstance);

                        var versionToAdd = dependencyMod.Versions
                            .First(v => v.Id == $"{dependency.Name}-{dependency.MaxVersion}");

                        versionToAdd.ModsForReason = [..versionInInstance.ModsForReason, version.Id];
                        InstallModsData.RequestedModVersions.Add(versionToAdd);
                        continue;
                    }

                    if (InstallModsData.RequestedModVersions.ToList()
                        .Exists(v => v.Identifier == dependency.Name))
                    {
                        var addedDepVersionIndex = dependencyMod.Versions.IndexOf(
                            InstallModsData.RequestedModVersions.First(v => v.Identifier == dependency.Name));

                        if (addedDepVersionIndex <= minDepVersionIndex && addedDepVersionIndex >= maxDepVersionIndex)
                        {
                            InstallModsData.RequestedModVersions.First(v => v.Identifier == dependency.Name)
                                .ModsForReason
                                .Add(version.Id);
                            continue;
                        }

                        var versionToAdd = dependencyMod.Versions
                            .First(v => v.Id == $"{dependency.Name}-{dependency.MaxVersion}");

                        versionToAdd.ModsForReason =
                            [..dependencyMod.Versions[addedDepVersionIndex].ModsForReason, version.Id];
                        dependencyMod.Versions[addedDepVersionIndex].ModsForReason = [];
                        InstallModsData.RequestedModVersions.Add(versionToAdd);

                        InstallModsData.RequestedModVersions.Remove(dependencyMod.Versions[addedDepVersionIndex]);
                    }
                    else
                    {
                        var versionToAdd = dependencyMod.Versions
                            .First(v => v.Id == $"{dependency.Name}-{dependency.MaxVersion}");
                        versionToAdd.ModsForReason = [version.Id];
                        InstallModsData.RequestedModVersions.Add(versionToAdd);
                    }

                    continue;
                }

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
                        versionToAdd.ModsForReason = [..versionInInstance.ModsForReason, version.Id];
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
                        versionToAdd.ModsForReason =
                            [..dependencyMod.Versions[addedDepVersionIndex].ModsForReason, version.Id];
                        dependencyMod.Versions[addedDepVersionIndex].ModsForReason = [];
                        InstallModsData.RequestedModVersions.Add(versionToAdd);

                        InstallModsData.RequestedModVersions.Remove(dependencyMod.Versions[addedDepVersionIndex]);
                    }
                    else
                    {
                        var versionToAdd = dependencyMod.Versions.First();
                        versionToAdd.ModsForReason = [version.Id];
                        InstallModsData.RequestedModVersions.Add(versionToAdd);
                    }

                    continue;
                }

                if (dependency.MaxVersion is not null)
                {
                    var maxDepVersion =
                        dependencyMod.Versions.FirstOrDefault(v =>
                            v.Id == $"{dependency.Name}-{dependency.MaxVersion}");

                    var maxDepVersionIndex = 0;

                    if (maxDepVersion is not null)
                        maxDepVersionIndex = dependencyMod.Versions.IndexOf(maxDepVersion);

                    if (InstallModsData.SelectedInstance.Mods.Exists(v => v.Identifier == dependency.Name))
                    {
                        var versionInInstance =
                            InstallModsData.SelectedInstance.Mods.First(v => v.Identifier == dependency.Name);

                        var installedVersionIndex = dependencyMod.Versions.IndexOf(versionInInstance);

                        if (installedVersionIndex >= maxDepVersionIndex) continue;

                        InstallModsData.RequestedRemoveModVersions.Add(versionInInstance);

                        var versionToAdd = dependencyMod.Versions
                            .First(v => v.Id == $"{dependency.Name}-{dependency.MaxVersion}");

                        versionToAdd.ModsForReason = [..versionInInstance.ModsForReason, version.Id];
                        InstallModsData.RequestedModVersions.Add(versionToAdd);

                        continue;
                    }

                    if (InstallModsData.RequestedModVersions.ToList()
                        .Exists(v => v.Identifier == dependency.Name))
                    {
                        var addedDepVersionIndex = dependencyMod.Versions.IndexOf(
                            InstallModsData.RequestedModVersions.First(v => v.Identifier == dependency.Name));

                        if (addedDepVersionIndex >= maxDepVersionIndex)
                        {
                            InstallModsData.RequestedModVersions.First(v => v.Identifier == dependency.Name)
                                .ModsForReason
                                .Add(version.Id);
                            continue;
                        }

                        var versionToAdd = dependencyMod.Versions
                            .First(v => v.Id == $"{dependency.Name}-{dependency.MaxVersion}");

                        versionToAdd.ModsForReason =
                            [..dependencyMod.Versions[addedDepVersionIndex].ModsForReason, version.Id];
                        dependencyMod.Versions[addedDepVersionIndex].ModsForReason = [];
                        InstallModsData.RequestedModVersions.Add(versionToAdd);

                        InstallModsData.RequestedModVersions.Remove(dependencyMod.Versions[addedDepVersionIndex]);
                    }
                    else
                    {
                        var versionToAdd = dependencyMod.Versions
                            .First(v => v.Id == $"{dependency.Name}-{dependency.MaxVersion}");

                        versionToAdd.ModsForReason = [version.Id];
                        InstallModsData.RequestedModVersions.Add(versionToAdd);
                    }

                    continue;
                }

                if (dependency.Version is not null)
                {
                    var depVersion =
                        dependencyMod.Versions.FirstOrDefault(v =>
                            v.Id == $"{dependency.Name}-{dependency.Version}");

                    var depVersionIndex = 0;

                    if (depVersion is not null)
                        depVersionIndex = dependencyMod.Versions.IndexOf(depVersion);

                    if (InstallModsData.SelectedInstance.Mods.Exists(v => v.Identifier == dependency.Name))
                    {
                        var versionInInstance =
                            InstallModsData.SelectedInstance.Mods.First(v => v.Identifier == dependency.Name);

                        var installedVersionIndex = dependencyMod.Versions.IndexOf(versionInInstance);

                        if (installedVersionIndex == depVersionIndex) continue;

                        var versionToAdd = dependencyMod.Versions
                            .First(v => v.Id == $"{dependency.Name}-{dependency.Version}");

                        versionToAdd.ModsForReason = [..versionInInstance.ModsForReason, version.Id];
                        InstallModsData.RequestedModVersions.Add(versionToAdd);

                        continue;
                    }

                    if (InstallModsData.RequestedModVersions.ToList()
                        .Exists(v => v.Identifier == dependency.Name))
                    {
                        var addedDepVersionIndex = dependencyMod.Versions.IndexOf(
                            InstallModsData.RequestedModVersions.First(v => v.Identifier == dependency.Name));

                        if (addedDepVersionIndex == depVersionIndex)
                        {
                            InstallModsData.RequestedModVersions.First(v => v.Identifier == dependency.Name)
                                .ModsForReason
                                .Add(version.Id);
                            continue;
                        }

                        var versionToAdd = dependencyMod.Versions
                            .First(v => v.Id == $"{dependency.Name}-{dependency.Version}");

                        versionToAdd.ModsForReason =
                            [..dependencyMod.Versions[addedDepVersionIndex].ModsForReason, version.Id];
                        dependencyMod.Versions[addedDepVersionIndex].ModsForReason = [];
                        InstallModsData.RequestedModVersions.Add(versionToAdd);

                        InstallModsData.RequestedModVersions.Remove(dependencyMod.Versions[addedDepVersionIndex]);
                    }
                    else
                    {
                        var versionToAdd = dependencyMod.Versions
                            .First(v => v.Id == $"{dependency.Name}-{dependency.Version}");

                        versionToAdd.ModsForReason = [version.Id];
                        InstallModsData.RequestedModVersions.Add(versionToAdd);
                    }

                    continue;
                }
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