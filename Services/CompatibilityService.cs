using System;
using System.Collections.Generic;
using System.Linq;
using KMRLauncherMvvm.Exceptions;
using KMRLauncherMvvm.Models;

namespace KMRLauncherMvvm.Services;

public class CompatibilityService(ModListService modListService)
{
    public ModVersion GetCompatibleVersionFromRelation(Relationship relationship, GameVersionCriteria? criteria)
    {
        var dependencyMod = modListService.Mods?.FirstOrDefault(m => m.Id == relationship.Name);
        if (dependencyMod is null || relationship.AnyOf is not null)
            throw new NotSupportedException("Relationship should be handled in a different way.");

        if (HasInvalidVersionFields(relationship))
            throw new InvalidRelationshipException(relationship);

        return dependencyMod.Versions.FirstOrDefault(v =>
                   SatisfiesVersionLimits(v.Version, relationship) && IsCompatible(v, criteria))
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

    public List<ModVersion> GetVersionsWithinLimits(string identifier, string? exact, string? min, string? max,
        GameVersionCriteria? criteria)
    {
        var mod = modListService.Mods?.FirstOrDefault(m => m.Id == identifier);
        if (mod is null) return [];

        return mod.Versions
            .Where(v => SatisfiesVersionLimits(v.Version, exact, min, max) && IsCompatible(v, criteria))
            .ToList();
    }

    public List<ModVersion> GetProvidingVersions(string identifier, string module, GameVersionCriteria? criteria)
    {
        var mod = modListService.Mods?.FirstOrDefault(m => m.Id == identifier);
        if (mod is null) return [];

        return mod.Versions
            .Where(v => v.Provides?.Contains(module) == true && IsCompatible(v, criteria))
            .ToList();
    }

    public List<ModVersion> GetVersionsFromModule(string module, GameVersionCriteria? criteria = null)
    {
        if (modListService.Mods is null) return [];

        return modListService.Mods
            .Select(m => m.Versions.FirstOrDefault(v =>
                v.Provides?.Contains(module) == true && IsCompatible(v, criteria)))
            .OfType<ModVersion>()
            .ToList();
    }

    public static List<ResolutionProblem> FindConflicts(IReadOnlyList<ModVersion> toInstall,
        IReadOnlyList<ModVersion> installed)
    {
        List<ResolutionProblem> problems = [];
        HashSet<string> reported = [];

        for (var i = 0; i < toInstall.Count; i++)
        {
            var mod = toInstall[i];

            var others = toInstall.Skip(i + 1).Select(m => (Mod: m, IsInstalled: false))
                .Concat(installed.Select(m => (Mod: m, IsInstalled: true)));

            foreach (var (other, isInstalled) in others)
            {
                if (other.Identifier == mod.Identifier) continue;
                if (!ConflictsWith(mod, other) && !ConflictsWith(other, mod)) continue;

                var pair = string.CompareOrdinal(mod.Identifier, other.Identifier) < 0
                    ? $"{mod.Identifier}|{other.Identifier}"
                    : $"{other.Identifier}|{mod.Identifier}";
                if (!reported.Add(pair)) continue;

                problems.Add(new ResolutionProblem
                {
                    Mod = mod.Identifier,
                    IsConflict = true,
                    Message = isInstalled
                        ? $"{mod.Identifier} {mod.Version} conflicts with {other.Identifier} {other.Version}, " +
                          $"which is already installed in this instance."
                        : $"{mod.Identifier} {mod.Version} and {other.Identifier} {other.Version} conflict, " +
                          $"so they can't be installed together."
                });
            }
        }

        return problems;
    }

    private static bool ConflictsWith(ModVersion mod, ModVersion other) =>
        mod.Conflicts?.Any(conflict => conflict.AnyOf is { Count: > 0 } anyOf
            ? anyOf.Any(e => MatchesModule(other, e.Name, e.Version, e.MinVersion, e.MaxVersion))
            : MatchesModule(other, conflict.Name, conflict.Version, conflict.MinVersion, conflict.MaxVersion)) == true;

    private static bool MatchesModule(ModVersion mod, string? name, string? exact, string? min, string? max) =>
        name is not null &&
        ((mod.Identifier == name && SatisfiesVersionLimits(mod.Version, exact, min, max)) ||
         mod.Provides?.Contains(name) == true);

    private static readonly (int, int, int) LowestKsp = (0, 0, 0);
    private static readonly (int, int, int) HighestKsp = (int.MaxValue, int.MaxValue, int.MaxValue);

    public static bool IsCompatible(ModVersion version, GameVersionCriteria? criteria)
    {
        if (criteria is null) return true;

        var (modMin, modMax) = GetKspRange(version);

        foreach (var exact in criteria.ExactVersions)
        {
            var point = LowerBound(exact);
            if (point.CompareTo(modMin) >= 0 && point.CompareTo(modMax) <= 0) return true;
        }

        if (version.KspVersionStrict) return false;

        foreach (var prefix in criteria.MinorPrefixes)
        {
            if (LowerBound(prefix).CompareTo(modMax) <= 0 && UpperBound(prefix).CompareTo(modMin) >= 0) return true;
        }

        return false;
    }

    private static ((int, int, int) min, (int, int, int) max) GetKspRange(ModVersion version)
    {
        if (version.KspVersion is not null)
        {
            return IsAny(version.KspVersion)
                ? (LowestKsp, HighestKsp)
                : (LowerBound(version.KspVersion), UpperBound(version.KspVersion));
        }

        var min = version.KspVersionMin is null || IsAny(version.KspVersionMin)
            ? LowestKsp
            : LowerBound(version.KspVersionMin);
        var max = version.KspVersionMax is null || IsAny(version.KspVersionMax)
            ? HighestKsp
            : UpperBound(version.KspVersionMax);

        return (min, max);
    }

    private static bool IsAny(string kspVersion) => kspVersion.Equals("any", StringComparison.OrdinalIgnoreCase);

    private static (int, int, int) LowerBound(string kspVersion)
    {
        var parts = ParseKspVersion(kspVersion);
        if (parts is null) return LowestKsp;

        return (Part(parts, 0, 0), Part(parts, 1, 0), Part(parts, 2, 0));
    }

    private static (int, int, int) UpperBound(string kspVersion)
    {
        var parts = ParseKspVersion(kspVersion);
        if (parts is null) return HighestKsp;

        return (Part(parts, 0, int.MaxValue), Part(parts, 1, int.MaxValue), Part(parts, 2, int.MaxValue));
    }

    private static int Part(List<int> parts, int index, int missing) => index < parts.Count ? parts[index] : missing;

    private static List<int>? ParseKspVersion(string kspVersion)
    {
        List<int> parts = [];

        foreach (var part in kspVersion.Trim().Split('.').Take(3))
        {
            if (!int.TryParse(part, out var number)) return null;
            parts.Add(number);
        }

        return parts.Count > 0 ? parts : null;
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