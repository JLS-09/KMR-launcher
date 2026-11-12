using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using KMRLauncherMvvm.Exceptions;
using KMRLauncherMvvm.Models;

namespace KMRLauncherMvvm.Services;

public class DependencyService(ModListService modListService, CompatibilityService compatibilityService)
{
    private sealed class DependencyResolutionState(Instance? instance)
    {
        public Instance? Instance { get; } = instance;

        public GameVersionCriteria? Criteria { get; } =
            instance is null ? null : GameVersionCriteria.FromInstance(instance);

        public List<PlannedModVersion> Resolved { get; } = [];
        public List<PlannedModVersion> ToRemove { get; } = [];
        public HashSet<Relationship> Choosable { get; } = [];
        public HashSet<string> IgnoredRecommendations { get; } = [];
        public List<ResolutionProblem> Problems { get; } = [];
    }

    public InstallModsData ResolveDependencies(InstallModsData installModsData)
    {
        installModsData.ChoosableVersions = [];
        installModsData.FinalModList = [];
        installModsData.RequestedRemoveModVersions = [];
        installModsData.ModsWithIgnoredRecommendations = [];
        installModsData.Problems = [];

        var instance = installModsData.SelectedInstance;
        if (modListService.Mods is null || instance is null) return installModsData;

        var dependencyResolutionState = new DependencyResolutionState(instance);

        foreach (var requested in installModsData.RequestedModVersions.Where(r =>
                     r.Version.Id is null || !instance.Mods.Exists(m => m.Id == r.Version.Id)))
        {
            dependencyResolutionState.Resolved.Add(requested);

            if (!CompatibilityService.IsCompatible(requested.Version, dependencyResolutionState.Criteria))
            {
                dependencyResolutionState.Problems.Add(new ResolutionProblem
                {
                    Mod = requested.Version.Identifier,
                    Message = $"You picked {requested.Version.Identifier} {requested.Version.Version}, which doesn't " +
                              $"support KSP {dependencyResolutionState.Criteria}. Pick another version, or add more " +
                              $"compatible KSP versions to this instance."
                });
            }
        }

        foreach (var requested in dependencyResolutionState.Resolved.ToList()
                     .Where(requested => dependencyResolutionState.Resolved.Contains(requested)))
        {
            ResolveRecursive(requested, dependencyResolutionState);
        }

        installModsData.FinalModList = new ObservableCollection<PlannedModVersion>(dependencyResolutionState.Resolved);
        installModsData.RequestedRemoveModVersions =
            new ObservableCollection<PlannedModVersion>(dependencyResolutionState.ToRemove);
        installModsData.ChoosableVersions = dependencyResolutionState.Choosable;
        installModsData.ModsWithIgnoredRecommendations =
            [.. dependencyResolutionState.IgnoredRecommendations];
        dependencyResolutionState.Problems.AddRange(CompatibilityService.FindConflicts(
            [.. dependencyResolutionState.Resolved.Select(r => r.Version)],
            RemainingInstalled(instance, dependencyResolutionState.Resolved, dependencyResolutionState.ToRemove)));
        installModsData.Problems = new ObservableCollection<ResolutionProblem>(dependencyResolutionState.Problems);
        return installModsData;
    }

    public sealed class AdditionalResolution
    {
        public required List<PlannedModVersion> FinalModList { get; init; }
        public required List<PlannedModVersion> ToRemove { get; init; }
        public required HashSet<Relationship> NewChoosables { get; init; }
        public required List<ResolutionProblem> Problems { get; init; }
    }

    public AdditionalResolution ResolveAdditional(Instance? instance,
        IEnumerable<PlannedModVersion> alreadyResolved, IEnumerable<PlannedModVersion> additions)
    {
        var state = new DependencyResolutionState(instance);
        state.Resolved.AddRange(alreadyResolved.Select(Copy));

        if (modListService.Mods is not null)
        {
            foreach (var addition in additions)
            {
                if (state.Resolved.Exists(w => w.Version.Identifier == addition.Version.Identifier)) continue;

                var copy = Copy(addition);
                state.Resolved.Add(copy);
                ResolveRecursive(copy, state);
            }
        }

        return new AdditionalResolution
        {
            FinalModList = state.Resolved,
            ToRemove = state.ToRemove,
            NewChoosables = state.Choosable,
            Problems = state.Problems
        };
    }

    private static PlannedModVersion Copy(PlannedModVersion wrapper) => new()
    {
        Version = wrapper.Version,
        IsUserRequested = wrapper.IsUserRequested,
        IsVersionPickedByUser = wrapper.IsVersionPickedByUser,
        Relationship = wrapper.Relationship,
        RequiredBy = [.. wrapper.RequiredBy]
    };

    public List<PlannedModVersion> GetDependencies(PlannedModVersion wrapper)
    {
        if (modListService.Mods is null) return [];

        var dependencyResolutionState = new DependencyResolutionState(null);
        dependencyResolutionState.Resolved.Add(wrapper);
        ResolveRecursive(wrapper, dependencyResolutionState);
        dependencyResolutionState.Resolved.Remove(wrapper);
        return dependencyResolutionState.Resolved;
    }

    private void ResolveRecursive(PlannedModVersion wrapper, DependencyResolutionState dependencyResolutionState)
    {
        if (wrapper.Version.Depends is not { Count: > 0 } depends) return;

        var reason = wrapper.Version.Id ?? wrapper.Version.Identifier;
        foreach (var dependency in depends)
        {
            try
            {
                ResolveDependency(dependency, reason, dependencyResolutionState);
            }
            catch (NoCompatibleVersionFoundException)
            {
                dependencyResolutionState.Problems.Add(
                    NoVersionProblem(dependency, reason, dependencyResolutionState.Criteria));
            }
            catch (InvalidRelationshipException)
            {
                dependencyResolutionState.Problems.Add(InvalidMetadataProblem(dependency, reason));
            }
            catch (Exception e) when (e is NotSupportedException or ArgumentException)
            {
                dependencyResolutionState.Problems.Add(new ResolutionProblem
                {
                    Mod = reason, Relationship = dependency,
                    Message = $"{reason} needs {dependency.Name}, which couldn't be resolved: {e.Message}"
                });
            }
        }
    }

    private void ResolveDependency(Relationship dependency, string reason,
        DependencyResolutionState dependencyResolutionState)
    {
        // Needs a user choice (any_of) or isn't a known mod
        if (dependency.AnyOf is not null || !ModExists(dependency.Name))
        {
            dependencyResolutionState.Choosable.Add(dependency);
            return;
        }

        if (dependency.Version is not null && (dependency.MinVersion is not null || dependency.MaxVersion is not null))
            throw new InvalidRelationshipException(dependency);

        if (dependency is { Name: not null, SuppressRecommendations: true })
        {
            dependencyResolutionState.IgnoredRecommendations.Add(dependency.Name);
        }

        var scheduled = dependencyResolutionState.Resolved.FirstOrDefault(v => v.Version.Identifier == dependency.Name);
        if (scheduled is not null)
        {
            if (compatibilityService.IsVersionCompatibleWithRelation(scheduled.Version, dependency))
            {
                scheduled.RequiredBy.Add(reason);
                return;
            }

            if (scheduled.IsUserRequested || scheduled.IsVersionPickedByUser)
            {
                dependencyResolutionState.Problems.Add(new ResolutionProblem
                {
                    Mod = reason, Relationship = dependency,
                    Message = $"You picked {scheduled.Version.Identifier} {scheduled.Version.Version}, but {reason} " +
                              $"needs {dependency.Name} {DescribeLimits(dependency)}."
                });
                return;
            }

            var combined = dependency;
            if (scheduled.Relationship is { AnyOf: null } previous && previous.Name == dependency.Name)
            {
                try
                {
                    combined = compatibilityService.CombineRelationships(previous, dependency);
                }
                catch (NoCompatibleVersionFoundException)
                {
                    dependencyResolutionState.Problems.Add(new ResolutionProblem
                    {
                        Mod = reason, Relationship = dependency,
                        Message = $"{string.Join(", ", scheduled.RequiredBy)} needs {dependency.Name} " +
                                  $"{DescribeLimits(previous)}, but {reason} needs {DescribeLimits(dependency)}."
                    });
                    return;
                }
            }

            var replacement = compatibilityService.GetCompatibleVersionFromRelation(combined,
                dependencyResolutionState.Criteria);
            dependencyResolutionState.Resolved.Remove(scheduled);
            AddDependency(combined, replacement, [.. scheduled.RequiredBy, reason], dependencyResolutionState);
            return;
        }

        var installed = dependencyResolutionState.Instance?.Mods.FirstOrDefault(v => v.Identifier == dependency.Name);
        if (installed is not null)
        {
            if (compatibilityService.IsVersionCompatibleWithRelation(installed, dependency)) return;

            var replacement = compatibilityService.GetCompatibleVersionFromRelation(dependency,
                dependencyResolutionState.Criteria);
            if (!dependencyResolutionState.ToRemove.Exists(w => w.Version.Id == installed.Id))
                dependencyResolutionState.ToRemove.Add(new PlannedModVersion { Version = installed });
            AddDependency(dependency, replacement, [reason], dependencyResolutionState);
            return;
        }

        var version = compatibilityService.GetCompatibleVersionFromRelation(dependency,
            dependencyResolutionState.Criteria);
        AddDependency(dependency, version, [reason], dependencyResolutionState);
    }

    private void AddDependency(Relationship dependency, ModVersion version, IEnumerable<string> reasons,
        DependencyResolutionState state)
    {
        var wrapper = new PlannedModVersion
        {
            Version = version,
            Relationship = dependency
        };
        wrapper.RequiredBy = [.. reasons];

        if (dependency is { Name: not null, SuppressRecommendations: true } && wrapper.Version.Depends is not null)
        {
            foreach (var depend in wrapper.Version.Depends.Where(d => d.Name is not null))
            {
                state.IgnoredRecommendations.Add(depend.Name!);
            }
        }

        state.Resolved.Add(wrapper);
        ResolveRecursive(wrapper, state);
    }

    private ResolutionProblem NoVersionProblem(Relationship dependency, string reason, GameVersionCriteria? criteria)
    {
        var withinLimits = compatibilityService.GetVersionsWithinLimits(dependency.Name ?? "", dependency.Version,
            dependency.MinVersion, dependency.MaxVersion, null);

        var message = withinLimits.Count > 0 && criteria is not null
            ? $"{reason} needs {dependency.Name}{LimitsSuffix(dependency)}, but no version of it supports KSP " +
              $"{criteria}. Add more compatible KSP versions to this instance?"
            : $"{reason} needs {dependency.Name} {DescribeLimits(dependency)}, but no such version exists.";

        return new ResolutionProblem { Mod = reason, Relationship = dependency, Message = message };
    }

    private static ResolutionProblem InvalidMetadataProblem(Relationship dependency, string reason) => new()
    {
        Mod = reason,
        Relationship = dependency,
        Message = $"{reason} has invalid metadata for {dependency.Name}: an exact version can't be combined with " +
                  $"min_version or max_version."
    };

    public static string DescribeLimits(Relationship relationship) =>
        (relationship.Version, relationship.MinVersion, relationship.MaxVersion) switch
        {
            ({ } exact, _, _) => $"version {exact}",
            (null, { } min, { } max) => $"version {min} to {max}",
            (null, { } min, null) => $"version {min} or newer",
            (null, null, { } max) => $"version {max} or older",
            _ => "(any version)"
        };

    private static string LimitsSuffix(Relationship relationship) =>
        relationship.Version is null && relationship.MinVersion is null && relationship.MaxVersion is null
            ? ""
            : $" {DescribeLimits(relationship)}";

    public static List<ModVersion> RemainingInstalled(Instance? instance, IEnumerable<PlannedModVersion> toInstall,
        IEnumerable<PlannedModVersion> toRemove)
    {
        if (instance is null) return [];

        var replaced = toInstall.Select(m => m.Version.Identifier).ToHashSet();
        var removed = toRemove.Select(m => m.Version.Id).ToHashSet();

        return instance.Mods.Where(m => !replaced.Contains(m.Identifier) && !removed.Contains(m.Id)).ToList();
    }
    
    private bool ModExists(string? name) => modListService.Mods?.Any(m => m.Id == name) == true;
}