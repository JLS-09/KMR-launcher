using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
    private sealed class RawChoice(Relationship relationship, List<string> requiredBy)
    {
        public Relationship Relationship { get; } = relationship;
        public List<string> RequiredBy { get; } = requiredBy;
        public List<ModVersionWrapper> Options { get; } = [];
    }

    private List<RawChoice> _rawChoices = [];

    private List<ModVersionWrapper> _baseFinalModList = [];

    [ObservableProperty] private ObservableCollection<ChoiceGroupViewModel> _choices = [];
    [ObservableProperty] private bool _hasChoices;
    [ObservableProperty] private bool _hasUnsatisfiableChoices;
    [ObservableProperty] private bool _allChoicesMade;

    public override bool CanGoNext => AllChoicesMade;

    partial void OnAllChoicesMadeChanged(bool value) => OnPropertyChanged(nameof(CanGoNext));

    public override void OnEntering(InstallModsStepViewModel? previous)
    {
        base.OnEntering(previous);

        foreach (var group in Choices)
            group.PropertyChanged -= OnChoiceGroupChanged;

        ProcessChoosables();
    }

    private void ProcessChoosables()
    {
        _rawChoices = [];

        EliminateDuplicates();
        foreach (var choosableVersion in InstallModsData.ChoosableVersions)
        {
            var choice = new RawChoice(choosableVersion, FindRequiredBy(choosableVersion));

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

                choice.Options.AddRange(anyOfVersions
                    .DistinctBy(v => v.Identifier)
                    .Select(v => CreateOption(v, choice)));
            }
            else if (choosableVersion.Name != null)
            {
                choice.Options.AddRange(compatibilityService.GetVersionsFromModule(choosableVersion.Name)
                    .Select(v => CreateOption(v, choice)));
            }

            _rawChoices.Add(choice);
        }

        EliminateAlreadyInstalled();
        EliminateAlreadyQueued();
        EliminateCoveredChoices();
        EliminateSingleChoices();

        _baseFinalModList = [.. InstallModsData.FinalModList];

        var groups = _rawChoices
            .Select(c => new ChoiceGroupViewModel(c.Relationship, c.Options, c.RequiredBy))
            .ToList();

        foreach (var group in groups)
            group.PropertyChanged += OnChoiceGroupChanged;

        Choices = new ObservableCollection<ChoiceGroupViewModel>(groups);
        HasChoices = Choices.Count > 0;
        HasUnsatisfiableChoices = Choices.Any(c => c.IsUnsatisfiable);
        UpdateAllChoicesMade();
    }

    private static ModVersionWrapper CreateOption(ModVersion version, RawChoice choice) => new()
    {
        Version = version,
        Relationship = choice.Relationship,
        ModsForReason = [.. choice.RequiredBy]
    };

    private List<string> FindRequiredBy(Relationship relationship)
    {
        return InstallModsData.FinalModList
            .Where(w => w.Version.Depends?.Any(d =>
                d.Equals(relationship) ||
                (relationship.AnyOf is null && d.AnyOf is null && d.Name == relationship.Name)) == true)
            .Select(w => w.Version.Id ?? w.Version.Identifier)
            .Distinct()
            .ToList();
    }

    private void OnChoiceGroupChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ChoiceGroupViewModel.SelectedOption)) return;

        ApplySelections();
        UpdateAllChoicesMade();
    }
    
    private void ApplySelections()
    {
        var finalModList = new ObservableCollection<ModVersionWrapper>(_baseFinalModList);

        foreach (var option in Choices.Select(c => c.SelectedOption).OfType<ModVersionWrapper>())
        {
            if (finalModList.Any(w => w.Version.Identifier == option.Version.Identifier)) continue;
            finalModList.Add(option);
        }

        InstallModsData.FinalModList = finalModList;
    }

    private void UpdateAllChoicesMade()
    {
        AllChoicesMade = Choices.All(c => !c.IsUnsatisfiable && c.SelectedOption is not null);
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

        foreach (var choice in _rawChoices.ToList())
        {
            if (choice.Options.Any(option => installed.Any(m => m.Identifier == option.Version.Identifier)))
                _rawChoices.Remove(choice);
        }
    }

    private void EliminateAlreadyQueued()
    {
        foreach (var choice in _rawChoices.ToList())
        {
            var resolved = false;

            foreach (var option in choice.Options.ToList())
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
                    choice.Options.Remove(option);
                }
            }

            if (resolved) _rawChoices.Remove(choice);
        }
    }

    private void EliminateSingleChoices()
    {
        foreach (var choice in _rawChoices.ToList())
        {
            if (choice.Options.Count != 1) continue;

            var option = choice.Options[0];

            var alreadyQueued = InstallModsData.FinalModList
                .Any(m => m.Version.Identifier == option.Version.Identifier);

            if (!alreadyQueued)
                InstallModsData.FinalModList.Add(option);

            _rawChoices.Remove(choice);
        }
    }

    private void EliminateCoveredChoices()
    {
        foreach (var choice in _rawChoices.ToList())
        {
            if (choice.Options.Count == 0) continue;

            var module = choice.Relationship.Name;
            if (module is null) continue;

            var coveredByOtherChoice = _rawChoices.Any(other =>
                other != choice && other.Options.Count > 0 &&
                other.Options.All(option => DependsOnProviderOf(option.Version, module)));

            if (coveredByOtherChoice) _rawChoices.Remove(choice);
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