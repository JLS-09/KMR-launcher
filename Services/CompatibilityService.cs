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
        var dependencyMod = modListService.Mods?.FirstOrDefault(m => m.Id == relationship.Name);
        if (dependencyMod is null || relationship.AnyOf is not null)
            throw new NotSupportedException("Relationship should be handled in a different way.");

        if (HasInvalidVersionFields(relationship))
            throw new InvalidRelationshipException(relationship);

        return dependencyMod.Versions.FirstOrDefault(v => SatisfiesVersionLimits(v.Version, relationship))
               ?? throw new NoCompatibleVersionFoundException(relationship);
    }

    public bool IsVersionCompatibleWithRelation(ModVersion version, Relationship relationship)
    {
        if (relationship.AnyOf is not null || version.Identifier != relationship.Name) return false;
        if (HasInvalidVersionFields(relationship)) return false;

        return SatisfiesVersionLimits(version.Version, relationship);
    }

    public static bool SatisfiesVersionLimits(string version, string? exact, string? min, string? max)
    {
        if (exact is not null) return CkanVersion.Compare(version, exact) == 0;
        if (min is not null && CkanVersion.Compare(version, min) < 0) return false;
        if (max is not null && CkanVersion.Compare(version, max) > 0) return false;
        return true;
    }

    public static bool SatisfiesVersionLimits(string version, Relationship relationship) =>
        SatisfiesVersionLimits(version, relationship.Version, relationship.MinVersion, relationship.MaxVersion);

    public static bool SatisfiesVersionLimits(string version, AnyOfEntry entry) =>
        SatisfiesVersionLimits(version, entry.Version, entry.MinVersion, entry.MaxVersion);

    private static bool HasInvalidVersionFields(Relationship relationship) =>
        relationship.Version is not null &&
        (relationship.MinVersion is not null || relationship.MaxVersion is not null);

    public List<ModVersion> GetVersionsWithinLimits(string identifier, string? exact, string? min, string? max)
    {
        var mod = modListService.Mods?.FirstOrDefault(m => m.Id == identifier);
        if (mod is null) return [];

        return mod.Versions.Where(v => SatisfiesVersionLimits(v.Version, exact, min, max)).ToList();
    }

    public List<ModVersion> GetProvidingVersions(string identifier, string module)
    {
        var mod = modListService.Mods?.FirstOrDefault(m => m.Id == identifier);
        if (mod is null) return [];

        return mod.Versions.Where(v => v.Provides?.Contains(module) == true).ToList();
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

        if (HasInvalidVersionFields(first)) throw new InvalidRelationshipException(first);
        if (HasInvalidVersionFields(second)) throw new InvalidRelationshipException(second);

        var mod = modListService.Mods?.FirstOrDefault(m => m.Id == first.Name);
        if (mod is null)
        {
            if (first.Name is not null && GetVersionsFromModule(first.Name).Count > 0)
                return CombineModuleRelationships(first, second);

            throw new NotSupportedException("Relationship should be handled in a different way.");
        }

        var combined = new Relationship
        {
            Name = first.Name,
            SuppressRecommendations = first.SuppressRecommendations == true || second.SuppressRecommendations == true
                ? true
                : null,
            Comment = CombineComments(first.Comment, second.Comment),
            ChoiceHelpText = first.ChoiceHelpText ?? second.ChoiceHelpText,
            MinVersion = PickNewest(first.MinVersion, second.MinVersion),
            MaxVersion = PickOldest(first.MaxVersion, second.MaxVersion)
        };

        if (first.Version is not null || second.Version is not null)
        {
            if (first.Version is not null && second.Version is not null &&
                CkanVersion.Compare(first.Version, second.Version) != 0)
                throw new NoCompatibleVersionFoundException(first);

            var exactVersion = first.Version ?? second.Version!;

            if (!SatisfiesVersionLimits(exactVersion, null, combined.MinVersion, combined.MaxVersion))
                throw new NoCompatibleVersionFoundException(combined);

            combined.Version = exactVersion;
            combined.MinVersion = null;
            combined.MaxVersion = null;
            return combined;
        }

        if (combined.MinVersion is not null && combined.MaxVersion is not null &&
            CkanVersion.Compare(combined.MinVersion, combined.MaxVersion) > 0)
            throw new NoCompatibleVersionFoundException(combined);

        return combined;
    }

    private static Relationship CombineModuleRelationships(Relationship first, Relationship second)
    {
        return new Relationship
        {
            Name = first.Name,
            SuppressRecommendations = first.SuppressRecommendations == true || second.SuppressRecommendations == true
                ? true
                : null,
            Comment = CombineComments(first.Comment, second.Comment),
            ChoiceHelpText = first.ChoiceHelpText ?? second.ChoiceHelpText,
            Version = PickSameVersion(first, first.Version, second.Version),
            MinVersion = PickSameVersion(first, first.MinVersion, second.MinVersion),
            MaxVersion = PickSameVersion(first, first.MaxVersion, second.MaxVersion)
        };
    }

    private static string? PickSameVersion(Relationship relationship, string? a, string? b)
    {
        if (a is null) return b;
        if (b is null) return a;
        if (a == b) return a;

        throw new NotSupportedException(
            $"Can't combine different version rules for module {relationship.Name}.");
    }

    private static string? PickNewest(string? a, string? b)
    {
        if (a is null) return b;
        if (b is null) return a;
        return CkanVersion.Compare(a, b) >= 0 ? a : b;
    }

    private static string? PickOldest(string? a, string? b)
    {
        if (a is null) return b;
        if (b is null) return a;
        return CkanVersion.Compare(a, b) <= 0 ? a : b;
    }

    private static string? CombineComments(string? a, string? b)
    {
        if (a is null) return b;
        if (b is null) return a;
        return $"{a} | {b}";
    }
}