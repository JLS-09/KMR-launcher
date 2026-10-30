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
        foreach (var choosableVersion in InstallModsData.ChoosableVersions)
        {
            // TODO remove choices where one option is already in finalModList
            // maybe also look at how versions are stored until now, it would be easier to to check compatibility if
            // we keep the relations in the wrapper perhaps so we can check version requirements
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