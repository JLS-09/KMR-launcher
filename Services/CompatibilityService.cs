using System.Linq;
using KMRLauncherMvvm.Models;

namespace KMRLauncherMvvm.Services;

public class CompatibilityService(ModListService modListService)
{
    public ModVersion GetCompatibleVersionFromRelation(Relationship relationship)
    {
        if (modListService.Mods is null || !modListService.Mods.ToList().Exists(m => m.Id == relationship.Name) ||
            relationship.AnyOf is not null) return null;

        var dependencyMod = modListService.Mods.First(m => m.Id == relationship.Name);

        if (relationship.Version is not null &&
            (relationship.MinVersion is not null || relationship.MaxVersion is not null)) return null;

        if (relationship.MaxVersion is null && relationship.MinVersion is null &&
            relationship.Version is null) return dependencyMod.Versions.First();

        if (relationship.MinVersion is not null && relationship.MaxVersion is not null)
        {
            var minCompatibleVersion =
                dependencyMod.Versions.FirstOrDefault(v =>
                    v.Id == $"{relationship.Name}-{relationship.MinVersion}");

            var maxCompatibleVersion =
                dependencyMod.Versions.FirstOrDefault(v =>
                    v.Id == $"{relationship.Name}-{relationship.MaxVersion}");

            int maxCompatibleVersionIndex;
            var minCompatibleVersionIndex = maxCompatibleVersionIndex = 0;

            if (minCompatibleVersion is not null && maxCompatibleVersion is not null)
            {
                minCompatibleVersionIndex = dependencyMod.Versions.IndexOf(minCompatibleVersion);
                maxCompatibleVersionIndex = dependencyMod.Versions.IndexOf(maxCompatibleVersion);
            }

            var compatibleVersions =
                dependencyMod.Versions.GetRange(minCompatibleVersionIndex,
                    minCompatibleVersionIndex - maxCompatibleVersionIndex + 1);

            return compatibleVersions
                .First(v => v.Id == $"{relationship.Name}-{relationship.MaxVersion}");
        }

        if (relationship.MinVersion is not null)
        {
            var minCompatibleVersion =
                dependencyMod.Versions.FirstOrDefault(v =>
                    v.Id == $"{relationship.Name}-{relationship.MinVersion}");

            var minCompatibleVersionIndex = 0;

            if (minCompatibleVersion is not null)
                minCompatibleVersionIndex = dependencyMod.Versions.IndexOf(minCompatibleVersion);

            var compatibleVersions =
                dependencyMod.Versions.GetRange(0, minCompatibleVersionIndex + 1);

            return compatibleVersions.First();
        }

        if (relationship.MaxVersion is not null)
        {
            var maxCompatibleVersion =
                dependencyMod.Versions.FirstOrDefault(v =>
                    v.Id == $"{relationship.Name}-{relationship.MaxVersion}");

            var maxCompatibleVersionIndex = 0;

            if (maxCompatibleVersion is not null)
                maxCompatibleVersionIndex = dependencyMod.Versions.IndexOf(maxCompatibleVersion);

            var compatibleVersions =
                dependencyMod.Versions.GetRange(maxCompatibleVersionIndex,
                    dependencyMod.Versions.Count - maxCompatibleVersionIndex);

            return compatibleVersions.First();
        }

        if (relationship.Version is not null)
        {
            var compatibleVersion =
                dependencyMod.Versions.FirstOrDefault(v =>
                    v.Id == $"{relationship.Name}-{relationship.Version}");

            if (compatibleVersion is not null)
                return compatibleVersion;
        }

        return null;
    }

    public bool IsVersionCompatibleWithRelation(ModVersion version, Relationship relationship)
    {
        var parentMod = modListService.Mods?.FirstOrDefault(m => m.Id.Equals(version.Identifier));

        if (parentMod is null) return false;

        var versionIndex = parentMod.Versions.IndexOf(version);

        if (relationship.Version is not null &&
            (relationship.MinVersion is not null || relationship.MaxVersion is not null)) return false;

        if (relationship.MaxVersion is null && relationship.MinVersion is null &&
            relationship.Version is null) return version.Identifier.Equals(relationship.Name);

        if (relationship.MinVersion is not null && relationship.MaxVersion is not null)
        {
            var relationMinVersionIndex = GetVersionIndex(parentMod, relationship.MinVersion);
            var relationMaxVersionIndex = GetVersionIndex(parentMod, relationship.MaxVersion);
            
            return versionIndex >= relationMaxVersionIndex && versionIndex <= relationMinVersionIndex ;
        }

        if (relationship.MinVersion is not null)
        {
            var relationMinVersionIndex = GetVersionIndex(parentMod, relationship.MinVersion);
            
            return versionIndex <= relationMinVersionIndex;
        }
        
        if (relationship.MinVersion is not null)
        {
            var relationMaxVersionIndex = GetVersionIndex(parentMod, relationship.MinVersion);
            
            return versionIndex >= relationMaxVersionIndex;
        }

        if (relationship.Version is not null)
        {
            var relationVersionIndex = GetVersionIndex(parentMod, relationship.Version);
            
            return versionIndex == relationVersionIndex;
        }

        return false;
    }

    private static int GetVersionIndex(Mod mod, string versionString)
    {
        var version =
            mod.Versions.FirstOrDefault(v =>
                v.Id == $"{mod.Name}-{versionString}");

        return version is not null ? mod.Versions.IndexOf(version) : 0;
    }
}