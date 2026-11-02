using System;
using System.Collections.Generic;
using System.Linq;
using KMRLauncherMvvm.Exceptions;
using KMRLauncherMvvm.Models;

namespace KMRLauncherMvvm.Services;

public class CompatibilityService(ModListService modListService)
{
    public ModVersion GetCompatibleVersionFromRelation(Relationship relationship)
    {
        if (modListService.Mods is null || !modListService.Mods.ToList().Exists(m => m.Id == relationship.Name) ||
            relationship.AnyOf is not null)
            throw new NotSupportedException("Relationship should be handled in a different way.");

        var dependencyMod = modListService.Mods.First(m => m.Id == relationship.Name);

        if (relationship.Version is not null &&
            (relationship.MinVersion is not null || relationship.MaxVersion is not null))
            throw new InvalidRelationshipException(relationship);

        if (relationship.MaxVersion is null && relationship.MinVersion is null &&
            relationship.Version is null) return dependencyMod.Versions.First();

        if (relationship.MinVersion is not null && relationship.MaxVersion is not null)
        {
            var maxCompatibleVersionIndex = GetVersionIndex(dependencyMod, relationship.MaxVersion);
            var minCompatibleVersionIndex = GetVersionIndex(dependencyMod, relationship.MinVersion);

            var compatibleVersions =
                dependencyMod.Versions.GetRange(maxCompatibleVersionIndex,
                    minCompatibleVersionIndex - maxCompatibleVersionIndex + 1);

            return compatibleVersions.First();
        }

        if (relationship.MinVersion is not null)
        {
            var minCompatibleVersionIndex = GetVersionIndex(dependencyMod, relationship.MinVersion);

            var compatibleVersions =
                dependencyMod.Versions.GetRange(0, minCompatibleVersionIndex + 1);

            return compatibleVersions.First();
        }

        if (relationship.MaxVersion is not null)
        {
            var maxCompatibleVersionIndex = GetVersionIndex(dependencyMod, relationship.MaxVersion);

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

        throw new NoCompatibleVersionFoundException(relationship);
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

            return versionIndex >= relationMaxVersionIndex && versionIndex <= relationMinVersionIndex;
        }

        if (relationship.MinVersion is not null)
        {
            var relationMinVersionIndex = GetVersionIndex(parentMod, relationship.MinVersion);

            return versionIndex <= relationMinVersionIndex;
        }

        if (relationship.MaxVersion is not null)
        {
            var relationMaxVersionIndex = GetVersionIndex(parentMod, relationship.MaxVersion);

            return versionIndex >= relationMaxVersionIndex;
        }

        if (relationship.Version is not null)
        {
            var relationVersionIndex = GetVersionIndex(parentMod, relationship.Version);

            return versionIndex == relationVersionIndex;
        }

        return false;
    }

    public List<ModVersion> GetVersionsFromModule(string module)
    {
        var versions = modListService.Mods!
            .Where(m => m.Versions.Any(v => v.Provides is not null && v.Provides.Contains(module))).Select(m =>
                m.Versions.First(v => v.Provides is not null && v.Provides.Contains(module))).ToHashSet();

        return [.. versions];
    }

    public Relationship CombineRelationships(Relationship first, Relationship second)
    {
        if (first.AnyOf is not null || second.AnyOf is not null)
            throw new NotSupportedException("Relationships with AnyOf can't be combined.");

        if (first.Name != second.Name)
            throw new ArgumentException("Only relationships for the same mod can be combined.");

        // A relationship can't have an exact version AND a min/max at the same time
        if (first.Version is not null && (first.MinVersion is not null || first.MaxVersion is not null))
            throw new InvalidRelationshipException(first);
        if (second.Version is not null && (second.MinVersion is not null || second.MaxVersion is not null))
            throw new InvalidRelationshipException(second);

        var mod = modListService.Mods?.FirstOrDefault(m => m.Id == first.Name);
        if (mod is null)
            throw new NotSupportedException("Relationship should be handled in a different way.");

        var combined = new Relationship
        {
            Name = first.Name,
            SuppressRecommendations = first.SuppressRecommendations == true || second.SuppressRecommendations == true
                ? true
                : null,
            Comment = CombineComments(first.Comment, second.Comment),
            // The strictest minimum is the NEWEST of the two
            MinVersion = PickNewest(mod, first.MinVersion, second.MinVersion),
            // The strictest maximum is the OLDEST of the two
            MaxVersion = PickOldest(mod, first.MaxVersion, second.MaxVersion)
        };

        // Case 1: at least one of them asks for an exact version
        if (first.Version is not null || second.Version is not null)
        {
            if (first.Version is not null && second.Version is not null && first.Version != second.Version)
                throw new NoCompatibleVersionFoundException(first);

            var exactVersion = first.Version ?? second.Version!;
            var exactIndex = FindVersionIndex(mod, exactVersion);

            // Remember: a higher index means an OLDER version
            if (combined.MinVersion is not null && exactIndex > FindVersionIndex(mod, combined.MinVersion))
                throw new NoCompatibleVersionFoundException(combined);

            if (combined.MaxVersion is not null && exactIndex < FindVersionIndex(mod, combined.MaxVersion))
                throw new NoCompatibleVersionFoundException(combined);

            // The exact version satisfies everything, so only keep that
            combined.Version = exactVersion;
            combined.MinVersion = null;
            combined.MaxVersion = null;
            return combined;
        }

        // Case 2: only ranges. Check that the min isn't newer than the max
        if (combined.MinVersion is not null && combined.MaxVersion is not null &&
            FindVersionIndex(mod, combined.MinVersion) < FindVersionIndex(mod, combined.MaxVersion))
            throw new NoCompatibleVersionFoundException(combined);

        return combined;
    }

    private static string? PickNewest(Mod mod, string? a, string? b)
    {
        if (a is null) return b;
        if (b is null) return a;
        return FindVersionIndex(mod, a) <= FindVersionIndex(mod, b) ? a : b;
    }

    private static string? PickOldest(Mod mod, string? a, string? b)
    {
        if (a is null) return b;
        if (b is null) return a;
        return FindVersionIndex(mod, a) >= FindVersionIndex(mod, b) ? a : b;
    }

    private static int FindVersionIndex(Mod mod, string versionString)
    {
        var index = mod.Versions.FindIndex(v => v.Version == versionString);

        if (index == -1)
            throw new ArgumentException($"Version {versionString} does not exist for mod {mod.Id}.");

        return index;
    }

    private static string? CombineComments(string? a, string? b)
    {
        if (a is null) return b;
        if (b is null) return a;
        return $"{a} | {b}";
    }

    private static int GetVersionIndex(Mod mod, string versionString)
    {
        var version =
            mod.Versions.FirstOrDefault(v =>
                v.Id == $"{mod.Id}-{versionString}");

        return version is not null ? mod.Versions.IndexOf(version) : 0;
    }
}