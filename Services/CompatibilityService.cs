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
                   SatisfiesVersionLimits(v.Version, relationship) && KspCompatibility.IsCompatible(v, criteria))
               ?? throw new NoCompatibleVersionFoundException(relationship);
    }

    public bool ModExists(string? identifier) => modListService.Mods?.Any(m => m.Id == identifier) == true;

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
            .Where(v => SatisfiesVersionLimits(v.Version, exact, min, max) && KspCompatibility.IsCompatible(v, criteria))
            .ToList();
    }

    public List<ModVersion> GetProvidingVersions(string identifier, string module, GameVersionCriteria? criteria)
    {
        var mod = modListService.Mods?.FirstOrDefault(m => m.Id == identifier);
        if (mod is null) return [];

        return mod.Versions
            .Where(v => v.Provides?.Contains(module) == true && KspCompatibility.IsCompatible(v, criteria))
            .ToList();
    }

    public List<ModVersion> GetVersionsFromModule(string module, GameVersionCriteria? criteria = null)
    {
        if (modListService.Mods is null) return [];

        return modListService.Mods
            .Select(m => m.Versions.FirstOrDefault(v =>
                v.Provides?.Contains(module) == true && KspCompatibility.IsCompatible(v, criteria)))
            .OfType<ModVersion>()
            .ToList();
    }

    public static List<ResolutionProblem> FindConflicts(IReadOnlyList<ModVersion> toInstall,
        IReadOnlyList<ModVersion> installed, IReadOnlyDictionary<string, string>? dlls = null)
    {
        List<ResolutionProblem> problems = [];
        HashSet<string> reported = [];

        foreach (var mod in toInstall)
        foreach (var name in (mod.Conflicts ?? []).SelectMany(Relationships.NamesIn).Distinct())
        {
            if (dlls?.TryGetValue(name, out var path) != true || !reported.Add($"{mod.Identifier}|{name}")) continue;

            problems.Add(new ResolutionProblem
            {
                Mod = mod.Identifier,
                IsConflict = true,
                Message = $"{mod.Identifier} {mod.Version} conflicts with {name}, which was installed by hand ({path})."
            });
        }

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

    public static List<ResolutionProblem> FindBrokenDepends(IReadOnlyList<ModVersion> toInstall,
        IReadOnlyList<ModVersion> remainingInstalled, IReadOnlyList<ModVersion> installedBefore,
        IReadOnlyDictionary<string, string>? dlls = null)
    {
        var after = Relationships.ByName([.. toInstall, .. remainingInstalled]);
        var before = Relationships.ByName(installedBefore);
        List<ResolutionProblem> problems = [];

        foreach (var mod in remainingInstalled)
        {
            foreach (var dependency in mod.Depends ?? [])
            {
                if (dependency.AnyOf is null && HasInvalidVersionFields(dependency)) continue;
                if (dlls is not null && Relationships.NamesIn(dependency).Any(dlls.ContainsKey)) continue;
                if (IsMet(dependency, after, mod) || !IsMet(dependency, before, mod)) continue;

                problems.Add(new ResolutionProblem
                {
                    Mod = mod.Identifier,
                    Relationship = dependency,
                    Message = DescribeBrokenDepend(mod, dependency, toInstall)
                });
            }
        }

        return problems;
    }

    private static bool IsMet(Relationship dependency, ILookup<string, ModVersion> mods, ModVersion owner) =>
        Relationships.Candidates(dependency, mods)
            .Any(m => m.Identifier != owner.Identifier && Relationships.Matches(dependency, m));

    private static string DescribeBrokenDepend(ModVersion mod, Relationship dependency,
        IReadOnlyList<ModVersion> toInstall)
    {
        var names = Relationships.NamesIn(dependency).ToHashSet();
        var hasLimits = dependency.Version is not null || dependency.MinVersion is not null ||
                        dependency.MaxVersion is not null;
        var needs = dependency.AnyOf is { Count: > 0 }
            ? $"one of {string.Join(", ", names)}"
            : hasLimits
                ? $"{dependency.Name} {DependencyService.DescribeLimits(dependency)}"
                : dependency.Name;
        var replacement = toInstall.FirstOrDefault(m => Relationships.NamesOf(m).Any(names.Contains));

        return replacement is not null
            ? $"{mod.Identifier} {mod.Version} is installed and needs {needs}, but this change installs " +
              $"{replacement.Identifier} {replacement.Version}. Pick another version, or update or remove " +
              $"{mod.Identifier} too."
            : $"{mod.Identifier} {mod.Version} is installed and needs {needs}, which this change removes.";
    }

    private static bool ConflictsWith(ModVersion mod, ModVersion other) =>
        mod.Conflicts?.Any(conflict => Relationships.Matches(conflict, other)) == true;

    public Relationship CombineRelationships(Relationship first, Relationship second)
    {
        if (first.AnyOf is not null || second.AnyOf is not null)
            throw new NotSupportedException("Relationships with AnyOf can't be combined.");

        if (first.Name != second.Name)
            throw new ArgumentException("Only relationships for the same mod can be combined.");

        if (HasInvalidVersionFields(first)) throw new InvalidRelationshipException(first);
        if (HasInvalidVersionFields(second)) throw new InvalidRelationshipException(second);

        var mod = modListService.Mods?.FirstOrDefault(m => m.Id == first.Name);
        if (mod is null) return CombineModuleRelationships(first, second);

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
            ChoiceHelpText = first.ChoiceHelpText ?? second.ChoiceHelpText
        };
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