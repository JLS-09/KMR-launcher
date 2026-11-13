using System.Collections.ObjectModel;
using KMRLauncherMvvm.Models;
using KMRLauncherMvvm.Services;

namespace KMRLauncherMvvm.Tests;

// Hand-written CKAN metadata for the tests
public static class TestData
{
    public static ModVersion Version(string identifier, string version, string? ksp = "1.12",
        string? kspMin = null, string? kspMax = null, bool strict = false,
        Relationship[]? depends = null, Relationship[]? conflicts = null, string[]? provides = null) => new()
    {
        Identifier = identifier,
        Version = version,
        Id = $"{identifier}-{version}",
        SpecVersion = "v1.34",
        License = ["MIT"],
        KspVersion = ksp,
        KspVersionMin = kspMin,
        KspVersionMax = kspMax,
        KspVersionStrict = strict,
        Depends = depends?.ToList(),
        Conflicts = conflicts?.ToList(),
        Provides = provides?.ToList()
    };

    // Versions must be given newest first, like GitHelper sorts them
    public static Mod Mod(params ModVersion[] versions) => new()
    {
        Id = versions[0].Identifier,
        Name = versions[0].Identifier,
        Abstract = "",
        Author = [],
        Versions = [.. versions]
    };

    public static Relationship Rel(string name, string? version = null, string? min = null, string? max = null,
        bool suppress = false) => new()
    {
        Name = name,
        Version = version,
        MinVersion = min,
        MaxVersion = max,
        SuppressRecommendations = suppress ? true : null
    };

    public static Relationship AnyOf(params AnyOfEntry[] entries) => new() { AnyOf = [.. entries] };

    public static AnyOfEntry Entry(string name, string? min = null, string? max = null) =>
        new() { Name = name, MinVersion = min, MaxVersion = max };

    public static Instance Instance(string kspVersion = "1.12.5", params ModVersion[] installed)
    {
        var instance = new Instance("Test", "/tmp/test", kspVersion);
        instance.Mods = [.. installed];
        return instance;
    }

    public static PlannedModVersion Requested(ModVersion version) => new() { Version = version, IsUserRequested = true };

    public sealed class TestServices(params Mod[] mods)
    {
        public ModListService ModList { get; } = new() { Mods = new ObservableCollection<Mod>(mods) };
        public CompatibilityService Compatibility => new(ModList);
        public DependencyService Dependency => new(ModList, Compatibility);
    }
}
