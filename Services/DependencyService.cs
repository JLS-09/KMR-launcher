using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using KMRLauncherMvvm.Models;

namespace KMRLauncherMvvm.Services;

public class DependencyService(ModListService modListService, CompatibilityService compatibilityService)
{
    private sealed class DependencyResolutionState(Instance? instance)
    {
        public Instance? Instance { get; } = instance;
        public List<ModVersionWrapper> Resolved { get; } = [];
        public List<ModVersionWrapper> ToRemove { get; } = [];
        public HashSet<Relationship> Choosable { get; } = [];
        public HashSet<string> IgnoredRecommendations { get; } = [];
    }

    public InstallModsData ResolveDependencies(InstallModsData installModsData)
    {
        installModsData.ChoosableVersions = [];
        installModsData.FinalModList = [];
        installModsData.RequestedRemoveModVersions = [];
        installModsData.ModsWithIgnoredRecommendations = [];

        var instance = installModsData.SelectedInstance;
        if (modListService.Mods is null || instance is null) return installModsData;

        var dependencyResolutionState = new DependencyResolutionState(instance);

        foreach (var requested in installModsData.RequestedModVersions.Where(r =>
                     r.Version.Id is null || !instance.Mods.Exists(m => m.Id == r.Version.Id)))
        {
            dependencyResolutionState.Resolved.Add(requested);
        }

        foreach (var requested in dependencyResolutionState.Resolved.ToList()
                     .Where(requested => dependencyResolutionState.Resolved.Contains(requested)))
        {
            ResolveRecursive(requested, dependencyResolutionState);
        }

        installModsData.FinalModList = new ObservableCollection<ModVersionWrapper>(dependencyResolutionState.Resolved);
        installModsData.RequestedRemoveModVersions =
            new ObservableCollection<ModVersionWrapper>(dependencyResolutionState.ToRemove);
        installModsData.ChoosableVersions = dependencyResolutionState.Choosable;
        installModsData.ModsWithIgnoredRecommendations =
            [.. dependencyResolutionState.IgnoredRecommendations];
        return installModsData;
    }

    public List<ModVersionWrapper> GetDependencies(ModVersionWrapper wrapper)
    {
        if (modListService.Mods is null) return [];

        var dependencyResolutionState = new DependencyResolutionState(null);
        dependencyResolutionState.Resolved.Add(wrapper);
        ResolveRecursive(wrapper, dependencyResolutionState);
        dependencyResolutionState.Resolved.Remove(wrapper);
        return dependencyResolutionState.Resolved;
    }

    private void ResolveRecursive(ModVersionWrapper wrapper, DependencyResolutionState dependencyResolutionState)
    {
        if (wrapper.Version.Depends is not { Count: > 0 } depends) return;

        var reason = wrapper.Version.Id ?? wrapper.Version.Identifier;
        foreach (var dependency in depends)
        {
            ResolveDependency(dependency, reason, dependencyResolutionState);
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

        if (dependency is { Name: not null, SuppressRecommendations: true })
        {
            dependencyResolutionState.IgnoredRecommendations.Add(dependency.Name);
        }

        if (dependency.Version is not null && (dependency.MinVersion is not null || dependency.MaxVersion is not null))
            return;

        // 1. Already scheduled for install in this run
        var scheduled = dependencyResolutionState.Resolved.FirstOrDefault(v => v.Version.Identifier == dependency.Name);
        if (scheduled is not null)
        {
            if (compatibilityService.IsVersionCompatibleWithRelation(scheduled.Version, dependency))
            {
                scheduled.ModsForReason.Add(reason);
                return;
            }

            dependencyResolutionState.Resolved.Remove(scheduled);
            AddDependency(dependency, [.. scheduled.ModsForReason, reason], dependencyResolutionState);
            return;
        }

        // 2. Already installed in the instance
        var installed = dependencyResolutionState.Instance?.Mods.FirstOrDefault(v => v.Identifier == dependency.Name);
        if (installed is not null)
        {
            if (compatibilityService.IsVersionCompatibleWithRelation(installed, dependency)) return;

            var existing = new ModVersionWrapper { Version = installed };
            if (!dependencyResolutionState.ToRemove.Exists(w => w.Version.Id == installed.Id))
                dependencyResolutionState.ToRemove.Add(existing);
            AddDependency(dependency, [reason], dependencyResolutionState);
            return;
        }

        // 3. Not present anywhere yet
        AddDependency(dependency, [reason], dependencyResolutionState);
    }

    private void AddDependency(Relationship dependency, IEnumerable<string> reasons, DependencyResolutionState state)
    {
        var wrapper = new ModVersionWrapper
        {
            Version = compatibilityService.GetCompatibleVersionFromRelation(dependency)
        };
        wrapper.ModsForReason = [.. reasons];

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

    private bool ModExists(string? name) => modListService.Mods?.Any(m => m.Id == name) == true;
}