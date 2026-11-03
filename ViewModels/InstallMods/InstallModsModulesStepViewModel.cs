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
            if (choosableVersion.AnyOf is not null && choosableVersion.AnyOf.Count > 0)
            {
                List<ModVersion> anyOfVersions = [];

                foreach (var anyOfEntry in choosableVersion.AnyOf)
                {
                    var relation = new Relationship
                    {
                        Name = anyOfEntry.Name, Version = anyOfEntry.Version,
                        MinVersion = anyOfEntry.MinVersion, MaxVersion = anyOfEntry.MaxVersion
                    };

                    try
                    {
                        anyOfVersions.Add(compatibilityService.GetCompatibleVersionFromRelation(relation));
                    }
                    catch (NotSupportedException)
                    {
                        anyOfVersions.AddRange(compatibilityService.GetVersionsFromModule(anyOfEntry.Name));
                    }
                    catch (NoCompatibleVersionFoundException)
                    {
                        // This option can't be installed, so skip it
                    }
                }

                anyOfVersions = [.. anyOfVersions.DistinctBy(v => v.Identifier)];

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

        EliminateAlreadyInstalled();
        EliminateAlreadyQueued();
        EliminateCoveredChoices();
        EliminateSingleChoices();

        foreach (var choice in Choices)
        {
            foreach (var option in choice)
            {
                Console.WriteLine(option.Version);
            }

            Console.WriteLine();
        }
    }

    private void EliminateDuplicates()
    {
        var duplicateGroups = InstallModsData.ChoosableVersions
            .Where(r => r.Name is not null && r.AnyOf is null)
            .GroupBy(r => r.Name)
            .Where(group => group.Count() > 1)
            .ToList();

        foreach (var group in duplicateGroups)
        {
            var relations = group.ToList();

            var combined = relations[0];
            for (var i = 1; i < relations.Count; i++)
            {
                combined = compatibilityService.CombineRelationships(combined, relations[i]);
            }

            foreach (var relation in relations)
            {
                InstallModsData.ChoosableVersions.Remove(relation);
            }

            InstallModsData.ChoosableVersions.Add(combined);
        }
    }

    private void EliminateAlreadyInstalled()
    {
        var installed = InstallModsData.SelectedInstance?.Mods;
        if (installed is null) return;

        foreach (var choice in Choices.ToList())
        {
            if (choice.Any(option => installed.Any(m => m.Identifier == option.Version.Identifier)))
                Choices.Remove(choice);
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
        foreach (var choice in Choices.ToList())
        {
            if (choice.Count != 1) continue;

            var option = choice[0];

            var alreadyQueued = InstallModsData.FinalModList
                .Any(m => m.Version.Identifier == option.Version.Identifier);

            if (!alreadyQueued)
                InstallModsData.FinalModList.Add(option);

            Choices.Remove(choice);
        }
    }

    private void EliminateCoveredChoices()
    {
        foreach (var choice in Choices.ToList())
        {
            var module = choice.FirstOrDefault()?.Relationship?.Name;
            if (module is null) continue;

            var coveredByOtherChoice = Choices.Any(other =>
                other != choice && other.Count > 0 &&
                other.All(option => DependsOnProviderOf(option.Version, module)));

            if (coveredByOtherChoice) Choices.Remove(choice);
        }
    }

    private bool DependsOnProviderOf(ModVersion version, string module)
    {
        if (version.Depends is null) return false;

        var providers = compatibilityService.GetVersionsFromModule(module);

        return version.Depends.Any(d => d.Name is not null &&
                                        (d.Name == module || providers.Any(p => p.Identifier == d.Name)));
    }

    public override string Title => "Choose Modules";
}