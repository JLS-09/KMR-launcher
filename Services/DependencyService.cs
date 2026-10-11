using System.Collections.Generic;
using System.Linq;
using KMRLauncherMvvm.Models;

namespace KMRLauncherMvvm.Services;

public class DependencyService(ModListService modListService)
{
    private readonly List<ModVersionWrapper> _addedDependencies = [];
    private readonly List<Relationship> _choosableVersions = [];

    public (List<ModVersionWrapper> resolvedDependencies, List<Relationship> choosableVersions) GetDependenciesOf(
        ModVersion modVersion)
    {
        _addedDependencies.Clear();
        _choosableVersions.Clear();
        
        _addedDependencies.Add(new ModVersionWrapper { Version = modVersion, IsUserRequested = true });
        ResolveDependenciesRecursive(modVersion);
        return ([.. _addedDependencies], _choosableVersions);
    }

    private void ResolveDependenciesRecursive(ModVersion modVersion)
    {
        if (modVersion.Id is null || modVersion.Depends is null || modVersion.Depends.Count == 0)
            return;

        foreach (var dependency in modVersion.Depends)
        {
            
        }
    }
}