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
    CompatibilityService compatibilityService,
    DependencyService dependencyService)
    : InstallModsStepViewModel(installModsData)
{
    private sealed class RawChoice(Relationship relationship, List<string> requiredBy)
    {
        public Relationship Relationship { get; } = relationship;
        public List<string> RequiredBy { get; } = requiredBy;
        public List<PlannedModVersion> Options { get; } = [];
    }

    private List<PlannedModVersion> _baseFinalModList = [];
    private List<PlannedModVersion> _baseRemoveList = [];
    private List<ChoiceGroupViewModel> _baseGroups = [];
    private List<PlannedModVersion> _baseAutoAdded = [];
    private List<ResolutionProblem> _baseProblems = [];

    private GameVersionCriteria? _criteria;

    private bool _isApplying;

    [ObservableProperty] private ObservableCollection<ChoiceGroupViewModel> _choices = [];
    [ObservableProperty] private bool _hasChoices;
    [ObservableProperty] private bool _hasUnsatisfiableChoices;
    [ObservableProperty] private bool _hasProblems;
    [ObservableProperty] private bool _allChoicesMade;

    public override bool CanGoNext => AllChoicesMade;

    partial void OnAllChoicesMadeChanged(bool value) => OnPropertyChanged(nameof(CanGoNext));

    public override void OnEntering(InstallModsStepViewModel? previous)
    {
        base.OnEntering(previous);

        foreach (var group in Choices)
            group.PropertyChanged -= OnChoiceGroupChanged;
        Choices = [];

        ProcessChoosables();
    }

    private void ProcessChoosables()
    {
        _criteria = InstallModsData.SelectedInstance is { } instance
            ? GameVersionCriteria.FromInstance(instance)
            : null;

        var queued = InstallModsData.FinalModList.ToList();
        List<ResolutionProblem> problems = [.. InstallModsData.Problems.Where(p => !p.IsConflict)];
        var rawChoices = BuildRawChoices(InstallModsData.ChoosableVersions, queued, problems, out var autoAdded);

        _baseProblems = problems;
        _baseFinalModList = queued;
        _baseRemoveList = [.. InstallModsData.RequestedRemoveModVersions];
        _baseAutoAdded = autoAdded;
        _baseGroups = [.. rawChoices.Select(CreateGroup)];

        ApplySelections();
    }

    private void ApplySelections()
    {
        _isApplying = true;
        try
        {
            var queued = _baseFinalModList.ToList();
            var removes = _baseRemoveList.ToList();
            var problems = _baseProblems.ToList();
            var desired = new List<ChoiceGroupViewModel>(_baseGroups);
            List<PlannedModVersion> pending = [.. _baseAutoAdded, .. SelectedOptions(_baseGroups)];

            while (pending.Count > 0)
            {
                var result = dependencyService.ResolveAdditional(InstallModsData.SelectedInstance, queued, pending);
                queued = result.FinalModList;
                problems.AddRange(result.Problems);

                foreach (var remove in result.ToRemove.Where(r => !removes.Exists(w => w.Version.Id == r.Version.Id)))
                    removes.Add(remove);

                var newChoosables = result.NewChoosables
                    .Where(r => !desired.Exists(g => g.Relationship.Equals(r)))
                    .ToHashSet();

                var rawChoices = BuildRawChoices(newChoosables, queued, problems, out var autoAdded);
                var newGroups = rawChoices
                    .Select(c => FindVisibleGroup(c.Relationship, desired) ?? CreateGroup(c))
                    .ToList();

                desired.AddRange(newGroups);
                pending = [.. autoAdded, .. SelectedOptions(newGroups)];
            }

            problems.AddRange(CompatibilityService.FindConflicts(
                [.. queued.Select(q => q.Version)],
                DependencyService.RemainingInstalled(InstallModsData.SelectedInstance, queued, removes)));

            InstallModsData.FinalModList = new ObservableCollection<PlannedModVersion>(queued);
            InstallModsData.RequestedRemoveModVersions = new ObservableCollection<PlannedModVersion>(removes);
            InstallModsData.Problems = new ObservableCollection<ResolutionProblem>(problems);

            SyncChoices(desired);
        }
        catch (Exception e)
        {
            InstallModsData.Problems =
            [
                .. InstallModsData.Problems,
                new ResolutionProblem { Mod = "", Message = $"Unexpected error: {e.Message}" }
            ];
        }
        finally
        {
            _isApplying = false;
        }

        HasChoices = Choices.Count > 0;
        HasUnsatisfiableChoices = Choices.Any(c => c.IsUnsatisfiable);
        HasProblems = InstallModsData.Problems.Count > 0;
        AllChoicesMade = !HasProblems && Choices.All(c => !c.IsUnsatisfiable && c.SelectedOption is not null);
    }

    private static IEnumerable<PlannedModVersion> SelectedOptions(IEnumerable<ChoiceGroupViewModel> groups) =>
        groups.Select(g => g.SelectedPlan).OfType<PlannedModVersion>();

    private ChoiceGroupViewModel? FindVisibleGroup(Relationship relationship, List<ChoiceGroupViewModel> desired) =>
        Choices.FirstOrDefault(g => g.Relationship.Equals(relationship) && !desired.Contains(g));

    private ChoiceGroupViewModel CreateGroup(RawChoice choice) =>
        new(choice.Relationship,
            choice.Options.Select(option => new ChoiceOptionViewModel(option, GetPickableVersions(option))),
            choice.RequiredBy);

    private List<ModVersion> GetPickableVersions(PlannedModVersion option)
    {
        var identifier = option.Version.Identifier;

        var anyOfEntry = option.Relationship?.AnyOf?.FirstOrDefault(a => a.Name == identifier);
        if (anyOfEntry is not null)
            return compatibilityService.GetVersionsWithinLimits(identifier, anyOfEntry.Version,
                anyOfEntry.MinVersion, anyOfEntry.MaxVersion, _criteria);

        var module = option.Relationship?.AnyOf?
                         .Select(a => a.Name)
                         .FirstOrDefault(name => option.Version.Provides?.Contains(name) == true)
                     ?? option.Relationship?.Name;

        return module is null
            ? [option.Version]
            : compatibilityService.GetProvidingVersions(identifier, module, _criteria);
    }

    private void SyncChoices(List<ChoiceGroupViewModel> desired)
    {
        foreach (var group in Choices.Where(g => !desired.Contains(g)).ToList())
        {
            group.PropertyChanged -= OnChoiceGroupChanged;
            Choices.Remove(group);
        }

        foreach (var group in desired.Where(g => !Choices.Contains(g)))
        {
            group.PropertyChanged += OnChoiceGroupChanged;
            Choices.Add(group);
        }
    }

    private void OnChoiceGroupChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isApplying || e.PropertyName != nameof(ChoiceGroupViewModel.SelectedPlan)) return;

        ApplySelections();
    }

    private List<RawChoice> BuildRawChoices(IEnumerable<Relationship> choosables, List<PlannedModVersion> queued,
        List<ResolutionProblem> problems, out List<PlannedModVersion> autoAdded)
    {
        var relationships = new HashSet<Relationship>(choosables);
        EliminateDuplicates(relationships, problems);

        List<RawChoice> choices = [];
        foreach (var relationship in relationships)
        {
            var choice = new RawChoice(relationship, FindRequiredBy(relationship, queued));

            if (relationship.AnyOf is not null && relationship.AnyOf.Count > 0)
            {
                List<ModVersion> anyOfVersions = [];

                foreach (var anyOfEntry in relationship.AnyOf)
                {
                    var relation = new Relationship
                    {
                        Name = anyOfEntry.Name, Version = anyOfEntry.Version,
                        MinVersion = anyOfEntry.MinVersion, MaxVersion = anyOfEntry.MaxVersion
                    };

                    try
                    {
                        anyOfVersions.Add(compatibilityService.GetCompatibleVersionFromRelation(relation, _criteria));
                    }
                    catch (NotSupportedException)
                    {
                        anyOfVersions.AddRange(compatibilityService.GetVersionsFromModule(anyOfEntry.Name, _criteria));
                    }
                    catch (NoCompatibleVersionFoundException)
                    {
                        // This option can't be installed, so skip it
                    }
                    catch (InvalidRelationshipException)
                    {
                        problems.Add(new ResolutionProblem
                        {
                            Mod = string.Join(", ", choice.RequiredBy), Relationship = relationship,
                            Message = $"{string.Join(", ", choice.RequiredBy)} has invalid metadata for " +
                                      $"{anyOfEntry.Name}: an exact version can't be combined with " +
                                      $"min_version or max_version."
                        });
                    }
                }

                choice.Options.AddRange(anyOfVersions
                    .DistinctBy(v => v.Identifier)
                    .Select(v => CreateOption(v, choice)));
            }
            else if (relationship.Name != null)
            {
                choice.Options.AddRange(compatibilityService.GetVersionsFromModule(relationship.Name, _criteria)
                    .Select(v => CreateOption(v, choice)));
            }

            choices.Add(choice);
        }

        EliminateAlreadyInstalled(choices);
        EliminateAlreadyQueued(choices, queued);
        EliminateCoveredChoices(choices);
        autoAdded = EliminateSingleChoices(choices, queued);

        return choices;
    }

    private static PlannedModVersion CreateOption(ModVersion version, RawChoice choice) => new()
    {
        Version = version,
        Relationship = choice.Relationship,
        RequiredBy = [.. choice.RequiredBy]
    };

    private static List<string> FindRequiredBy(Relationship relationship, List<PlannedModVersion> queued)
    {
        return queued
            .Where(w => w.Version.Depends?.Any(d =>
                d.Equals(relationship) ||
                (relationship.AnyOf is null && d.AnyOf is null && d.Name == relationship.Name)) == true)
            .Select(w => w.Version.Id ?? w.Version.Identifier)
            .Distinct()
            .ToList();
    }

    private void EliminateDuplicates(HashSet<Relationship> relationships, List<ResolutionProblem> problems)
    {
        var duplicateGroups = relationships
            .Where(r => r.Name is not null && r.AnyOf is null)
            .GroupBy(r => r.Name)
            .Where(group => group.Count() > 1)
            .ToList();

        foreach (var group in duplicateGroups)
        {
            var relations = group.ToList();

            Relationship combined;
            try
            {
                combined = relations[0];
                for (var i = 1; i < relations.Count; i++)
                {
                    combined = compatibilityService.CombineRelationships(combined, relations[i]);
                }
            }
            catch (Exception e) when (e is NoCompatibleVersionFoundException or NotSupportedException
                                          or InvalidRelationshipException)
            {
                problems.Add(new ResolutionProblem
                {
                    Mod = group.Key!, Relationship = relations[0],
                    Message = $"Mods need incompatible versions of {group.Key}: " +
                              $"{string.Join("; ", relations.Select(DependencyService.DescribeLimits))}."
                });
                continue;
            }

            foreach (var relation in relations)
            {
                relationships.Remove(relation);
            }

            relationships.Add(combined);
        }
    }

    private void EliminateAlreadyInstalled(List<RawChoice> choices)
    {
        var installed = InstallModsData.SelectedInstance?.Mods;
        if (installed is null || installed.Count == 0) return;

        choices.RemoveAll(choice => installed.Any(mod => IsSatisfiedBy(mod, choice.Relationship)));
    }

    private static bool IsSatisfiedBy(ModVersion mod, Relationship relationship)
    {
        return relationship.AnyOf is { Count: > 0 } anyOf
            ? anyOf.Any(entry => Matches(entry.Name, entry.Version, entry.MinVersion, entry.MaxVersion))
            : Matches(relationship.Name, relationship.Version, relationship.MinVersion, relationship.MaxVersion);

        bool Matches(string? name, string? exact, string? min, string? max) =>
            name is not null &&
            ((mod.Identifier == name && CompatibilityService.SatisfiesVersionLimits(mod.Version, exact, min, max)) ||
             mod.Provides?.Contains(name) == true);
    }

    private void EliminateAlreadyQueued(List<RawChoice> choices, List<PlannedModVersion> queued)
    {
        foreach (var choice in choices.ToList())
        {
            var resolved = false;

            foreach (var option in choice.Options.ToList())
            {
                var existing = queued.FirstOrDefault(m => m.Version.Identifier == option.Version.Identifier);

                if (existing is null) continue;

                var anyOfEntry = option.Relationship?.AnyOf?
                    .FirstOrDefault(a => a.Name == option.Version.Identifier);

                if (existing.IsUserRequested || existing.IsVersionPickedByUser)
                {
                    if (anyOfEntry is null ||
                        CompatibilityService.SatisfiesVersionLimits(existing.Version.Version, anyOfEntry))
                    {
                        resolved = true;
                        break;
                    }

                    choice.Options.Remove(option);
                    continue;
                }

                var existingRelation = existing.Relationship is not null &&
                                       existing.Relationship.AnyOf is null &&
                                       existing.Relationship.Name == existing.Version.Identifier
                    ? existing.Relationship
                    : new Relationship { Name = existing.Version.Identifier, Version = existing.Version.Version };

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
                    var newVersion = compatibilityService.GetCompatibleVersionFromRelation(combined, _criteria);

                    var index = queued.IndexOf(existing);
                    queued[index] = new PlannedModVersion
                    {
                        Version = newVersion,
                        RequiredBy = [.. existing.RequiredBy],
                        Relationship = combined
                    };

                    resolved = true;
                    break;
                }
                catch (Exception e) when (e is NoCompatibleVersionFoundException or NotSupportedException
                                              or InvalidRelationshipException)
                {
                    choice.Options.Remove(option);
                }
            }

            if (resolved) choices.Remove(choice);
        }
    }

    private static List<PlannedModVersion> EliminateSingleChoices(List<RawChoice> choices,
        List<PlannedModVersion> queued)
    {
        List<PlannedModVersion> autoAdded = [];

        foreach (var choice in choices.ToList())
        {
            if (choice.Options.Count != 1) continue;

            var option = choice.Options[0];

            var alreadyQueued = queued.Exists(m => m.Version.Identifier == option.Version.Identifier) ||
                                autoAdded.Exists(m => m.Version.Identifier == option.Version.Identifier);

            if (!alreadyQueued)
                autoAdded.Add(option);

            choices.Remove(choice);
        }

        return autoAdded;
    }

    private void EliminateCoveredChoices(List<RawChoice> choices)
    {
        foreach (var choice in choices.ToList())
        {
            if (choice.Options.Count == 0) continue;

            var module = choice.Relationship.Name;
            if (module is null) continue;

            var coveredByOtherChoice = choices.Any(other =>
                other != choice && other.Options.Count > 0 &&
                other.Options.All(option => DependsOnProviderOf(option.Version, module)));

            if (coveredByOtherChoice) choices.Remove(choice);
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