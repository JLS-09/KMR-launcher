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
        base.OnEntering(previous);
        ProcessChoosables();
    }

    private void ProcessChoosables()
    {
        EliminateDuplicates();
        foreach (var choosableVersion in InstallModsData.ChoosableVersions)
        {
            if (choosableVersion.AnyOf is not null && choosableVersion.AnyOf.Count > 0)
            {
                List<ModVersion> anyOfVersions =
                [
                    .. choosableVersion.AnyOf.Select(anyOfEntry =>
                        compatibilityService.GetCompatibleVersionFromRelation(new Relationship
                        {
                            Name = anyOfEntry.Name, Version = anyOfEntry.Version, MinVersion = anyOfEntry.MinVersion,
                            MaxVersion = anyOfEntry.MaxVersion
                        }))
                ];

                Choices.Add(anyOfVersions);
            }
            else if (choosableVersion.Name != null)
            {
                Choices.Add(compatibilityService.GetVersionsFromModule(choosableVersion.Name));
            }
        }

        EliminateSingleChoices();
    }

    private void EliminateDuplicates()
    {
        // TODO take another look at this because this doesnt look right at all
        // yeah this is actually very wrong but the combineRelationships is actually useful
        foreach (var choosableVersion in InstallModsData.ChoosableVersions)
        {
            if (InstallModsData.FinalModList.Any(v =>
                    v.Relationship is not null && v.Version.Identifier == choosableVersion.Name))
            {
                var duplicateVersion = InstallModsData.FinalModList.First(v =>
                    v.Relationship is not null && v.Version.Identifier == choosableVersion.Name);

                if (!compatibilityService.IsVersionCompatibleWithRelation(duplicateVersion.Version, choosableVersion))
                {
                    var newRelationship =
                        compatibilityService.CombineRelationships(duplicateVersion.Relationship!, choosableVersion);

                    var newVersion = compatibilityService.GetCompatibleVersionFromRelation(newRelationship);

                    InstallModsData.FinalModList.Remove(duplicateVersion);
                    InstallModsData.FinalModList.Add(new ModVersionWrapper
                        { Version = newVersion, Relationship = newRelationship });
                }
            }
        }
    }

    private void EliminateSingleChoices()
    {
        foreach (var choice in Choices)
        {
            if (choice.Count == 1)
            {
                // TODO remove choices where there is only one option and add to finalList
            }
        }
    }

    public override string Title => "Choose Modules";
}