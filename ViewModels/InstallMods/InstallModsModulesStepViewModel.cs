using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using KMRLauncherMvvm.Models;
using KMRLauncherMvvm.Services;

namespace KMRLauncherMvvm.ViewModels.InstallMods;

public partial class InstallModsModulesStepViewModel(
    InstallModsData installModsData,
    CompatibilityService compatibilityService)
    : InstallModsStepViewModel(installModsData)
{
    [ObservableProperty] private List<List<ModVersion>> _choices = [];

    public override void OnEntering(InstallModsStepViewModel? previous)
    {
        ProcessChoosables();
        base.OnEntering(previous);
    }

    private void ProcessChoosables()
    {
        HashSet<Relationship> anyOfs = [];
        HashSet<string> modules = [];

        foreach (var choosableVersion in InstallModsData.ChoosableVersions)
        {
            if (choosableVersion.AnyOf is not null) anyOfs.Add(choosableVersion);
            else if (choosableVersion.Name != null) modules.Add(choosableVersion.Name);
        }

        foreach (var relationship in anyOfs)
        {
            if (relationship.AnyOf is null || relationship.AnyOf.Count == 0) continue;

            List<ModVersion> anyOfVersions =
            [
                .. relationship.AnyOf.Select(anyOfEntry =>
                    compatibilityService.GetCompatibleVersionFromRelation(new Relationship
                    {
                        Name = anyOfEntry.Name, Version = anyOfEntry.Version, MinVersion = anyOfEntry.MinVersion,
                        MaxVersion = anyOfEntry.MaxVersion
                    }))
            ];

            Choices.Add(anyOfVersions);
        }

        foreach (var module in modules) Choices.Add(compatibilityService.GetVersionsFromModule(module));
    }

    public override string Title => "Choose Modules";
}