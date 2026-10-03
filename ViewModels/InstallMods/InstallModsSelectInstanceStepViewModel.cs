using System;
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

    public InstallModsSelectInstanceStepViewModel(InstallModsData installModsData, ModListService modListService,
        CompatibilityService compatibilityService) :
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
        InstallModsData.FinalModList.Clear();
        if (_modListService.Mods is null || InstallModsData.SelectedInstance is null) return;

        InstallModsData.FinalModList = [.. InstallModsData.RequestedModVersions];

        foreach (var version in InstallModsData.FinalModList.Where(version =>
                     InstallModsData.SelectedInstance.Mods.Exists(m => m.Id == version.Identifier)))
        {
            InstallModsData.FinalModList.Remove(version);
        }

        var i = 0;
        while (i < InstallModsData.FinalModList.Count)
        {
            var version = InstallModsData.FinalModList[i];

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

                if (dependency is { Name: not null, SuppressRecommendations: not null } &&
                    (bool)dependency.SuppressRecommendations)
                {
                    InstallModsData.ModsWithIgnoredRecommendations.Add(dependency.Name);
                }

                if (dependency.Version is not null &&
                    (dependency.MinVersion is not null || dependency.MaxVersion is not null)) continue;

                if (InstallModsData.SelectedInstance.Mods.Exists(v => v.Identifier == dependency.Name))
                {
                    var existingVersion =
                        InstallModsData.SelectedInstance.Mods.First(v => v.Identifier == dependency.Name);

                    if (_compatibilityService.IsVersionCompatibleWithRelation(existingVersion, dependency))
                    {
                        existingVersion.ModsForReason.Add(version.Id);
                        continue;
                    }

                    InstallModsData.RequestedRemoveModVersions.Add(existingVersion);
                    var versionToAdd = _compatibilityService.GetCompatibleVersionFromRelation(dependency);

                    if (dependency is { Name: not null, SuppressRecommendations: not null } &&
                        (bool)dependency.SuppressRecommendations && versionToAdd.Depends is not null)
                    {
                        foreach (var depend in versionToAdd.Depends)
                        {
                            if (depend.Name is not null)
                                InstallModsData.ModsWithIgnoredRecommendations.Add(depend.Name);
                        }
                    }

                    versionToAdd.ModsForReason = [.. existingVersion.ModsForReason, version.Id];
                    InstallModsData.FinalModList.Add(versionToAdd);
                    continue;
                }

                if (InstallModsData.FinalModList.ToList()
                    .Exists(v => v.Identifier == dependency.Name))
                {
                    var existingVersion = InstallModsData.FinalModList.ToList()
                        .First(v => v.Identifier == dependency.Name);

                    if (_compatibilityService.IsVersionCompatibleWithRelation(existingVersion, dependency))
                    {
                        if (InstallModsData.RequestedModVersions.Any(v => v.Identifier == existingVersion.Identifier))
                        {
                            continue;
                        }

                        existingVersion.ModsForReason.Add(version.Id);
                        continue;
                    }

                    InstallModsData.FinalModList.Remove(existingVersion);
                    var versionToAdd = _compatibilityService.GetCompatibleVersionFromRelation(dependency);

                    if (dependency is { Name: not null, SuppressRecommendations: not null } &&
                        (bool)dependency.SuppressRecommendations && versionToAdd.Depends is not null)
                    {
                        foreach (var depend in versionToAdd.Depends)
                        {
                            if (depend.Name is not null)
                                InstallModsData.ModsWithIgnoredRecommendations.Add(depend.Name);
                        }
                    }

                    versionToAdd.ModsForReason = [.. existingVersion.ModsForReason, version.Id];
                    InstallModsData.FinalModList.Add(versionToAdd);
                    continue;
                }

                var compatibleVersion = _compatibilityService.GetCompatibleVersionFromRelation(dependency);

                if (dependency is { Name: not null, SuppressRecommendations: not null } &&
                    (bool)dependency.SuppressRecommendations && compatibleVersion.Depends is not null)
                {
                    foreach (var depend in compatibleVersion.Depends)
                    {
                        if (depend.Name is not null) InstallModsData.ModsWithIgnoredRecommendations.Add(depend.Name);
                    }
                }

                compatibleVersion.ModsForReason = [version.Id];
                InstallModsData.FinalModList.Add(compatibleVersion);
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