using System.Collections.Generic;
using System.Linq;
using KMRLauncherMvvm.Models;

namespace KMRLauncherMvvm.Services;

public static class Relationships
{
    public static IEnumerable<string> NamesOf(ModVersion mod) => [mod.Identifier, .. mod.Provides ?? []];

    public static IEnumerable<string> NamesIn(Relationship relationship) =>
        relationship.AnyOf?.Select(e => e.Name).OfType<string>() ??
        (relationship.Name is null ? [] : [relationship.Name]);

    public static bool Names(Relationship relationship, ModVersion mod) =>
        Options(relationship).Any(o => o.Name is not null &&
                                       (mod.Identifier == o.Name || mod.Provides?.Contains(o.Name) == true));

    public static bool Matches(Relationship relationship, ModVersion mod) =>
        Options(relationship).Any(o => o.Name is not null &&
                                       ((mod.Identifier == o.Name &&
                                         CompatibilityService.SatisfiesVersionLimits(mod.Version, o.Version,
                                             o.MinVersion, o.MaxVersion)) ||
                                        mod.Provides?.Contains(o.Name) == true));

    private static IEnumerable<(string? Name, string? Version, string? MinVersion, string? MaxVersion)> Options(
        Relationship relationship) =>
        relationship.AnyOf is { Count: > 0 } anyOf
            ? anyOf.Select(e => ((string?)e.Name, e.Version, e.MinVersion, e.MaxVersion))
            : [(relationship.Name, relationship.Version, relationship.MinVersion, relationship.MaxVersion)];
}
