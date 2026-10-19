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
    }

    private void EliminateDuplicates()
    {
        foreach (var choice in Choices)
        {
            // TODO remove choices where one option is already in finalModList
        }
    }

    private void EliminateSingleChoices()
    {
        foreach (var choice in Choices)
        {
            if (choice.Count == 1)
            {
                // TODO remove choices where there is only one option
            }
        }
    }

    public override string Title => "Choose Modules";
}