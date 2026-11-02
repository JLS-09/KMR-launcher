using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using KMRLauncherMvvm.Exceptions;
using KMRLauncherMvvm.Models;
using KMRLauncherMvvm.Services;

namespace KMRLauncherMvvm.ViewModels.InstallMods;

public partial class InstallModsModulesStepViewModel(
    InstallModsData installModsData,
    CompatibilityService compatibilityService)
    : InstallModsStepViewModel(installModsData)
{
    [ObservableProperty] private List<List<ModVersionWrapper>> _choices = [];

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
            Console.WriteLine(choosableVersion.Name);
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

                Choices.Add([
                    .. anyOfVersions.Select(v => new ModVersionWrapper { Version = v, Relationship = choosableVersion })
                ]);
            }
            else if (choosableVersion.Name != null)
            {
                Choices.Add([
                    .. compatibilityService.GetVersionsFromModule(choosableVersion.Name).Select(v =>
                        new ModVersionWrapper { Version = v, Relationship = choosableVersion })
                ]);
            }
        }

        EliminateAlreadyQueued();
        EliminateSingleChoices();
    }

    private void EliminateDuplicates()
    {
        foreach (var choice in InstallModsData.ChoosableVersions.ToList())
        {
            // remove duplicates Like FAR in rp-1 express install from the list of choosable versions
        }
    }

    private void EliminateAlreadyQueued()
    {
        foreach (var choice in Choices.ToList())
        {
            var resolved = false;

            foreach (var option in choice.ToList())
            {
                var existing = InstallModsData.FinalModList
                    .FirstOrDefault(m => m.Version.Identifier == option.Version.Identifier);

                if (existing is null) continue;

                var existingRelation = existing.Relationship is not null &&
                                       existing.Relationship.AnyOf is null &&
                                       existing.Relationship.Name == existing.Version.Identifier
                    ? existing.Relationship
                    : new Relationship { Name = existing.Version.Identifier, Version = existing.Version.Version };

                var anyOfEntry = option.Relationship?.AnyOf?
                    .FirstOrDefault(a => a.Name == option.Version.Identifier);

                var choiceRelation = anyOfEntry is not null
                    ? new Relationship
                    {
                        Name = anyOfEntry.Name,
                        Version = anyOfEntry.Version,
                        MinVersion = anyOfEntry.MinVersion,
                        MaxVersion = anyOfEntry.MaxVersion
                    }
                    : new Relationship { Name = option.Version.Identifier };

                try
                {
                    var combined = compatibilityService.CombineRelationships(existingRelation, choiceRelation);
                    var newVersion = compatibilityService.GetCompatibleVersionFromRelation(combined);

                    var index = InstallModsData.FinalModList.IndexOf(existing);
                    InstallModsData.FinalModList[index] = new ModVersionWrapper
                    {
                        Version = newVersion,
                        IsUserRequested = existing.IsUserRequested,
                        ModsForReason = existing.ModsForReason,
                        Relationship = combined
                    };

                    resolved = true;
                    break;
                }
                catch (NoCompatibleVersionFoundException)
                {
                    choice.Remove(option);
                }
            }

            if (resolved) Choices.Remove(choice);
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