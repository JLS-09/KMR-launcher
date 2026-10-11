using System.Collections.Generic;
using KMRLauncherMvvm.Models;

namespace KMRLauncherMvvm.Services;

public class DependencyService
{
    private readonly List<ModVersion> _addedDependencies = [];

    public List<ModVersion> GetDependenciesOf(ModVersion modVersion)
    {
        _addedDependencies.Clear();
        _addedDependencies.Add(modVersion);
        ResolveDependenciesRecursive(modVersion);
        return [.. _addedDependencies];
    }

    private void ResolveDependenciesRecursive(ModVersion modVersion)
    {
        if (modVersion.Depends is null || modVersion.Depends.Count == 0)
            return;

        foreach (var dependency in modVersion.Depends)
        {
            
        }
    }
}