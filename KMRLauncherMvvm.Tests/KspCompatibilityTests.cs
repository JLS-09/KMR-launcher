using KMRLauncherMvvm.Models;
using KMRLauncherMvvm.Services;
using static KMRLauncherMvvm.Tests.TestData;

namespace KMRLauncherMvvm.Tests;

// Spec ksp_version, ksp_version_min/max, ksp_version_strict + per-instance compatible versions
public class KspCompatibilityTests
{
    private static readonly GameVersionCriteria OnlyInstance = new("1.12.5", []);

    private static bool Compatible(ModVersion version, GameVersionCriteria criteria) =>
        CompatibilityService.IsCompatible(version, criteria);

    [Theory]
    [InlineData(null, null, null, true)] // missing = any
    [InlineData("any", null, null, true)]
    [InlineData("1.12", null, null, true)] // 2-part = every 1.12.x
    [InlineData("1.11", null, null, false)]
    [InlineData("1.12.5", null, null, true)]
    [InlineData("1.12.4", null, null, false)] // no "generally recognised" rule: the user's list takes that role
    [InlineData(null, "1.8", "1.12", true)]
    [InlineData(null, "1.8", "1.11", false)]
    [InlineData(null, "1.12.5", null, true)]
    [InlineData(null, "1.12.6", null, false)]
    [InlineData(null, null, "1.12.9", true)]
    [InlineData("1.12.5.3190", null, null, true)] // build number ignored
    [InlineData("garbage", null, null, true)] // unreadable bound stays open
    public void Mod_range_against_instance_version(string? ksp, string? min, string? max, bool expected)
    {
        Assert.Equal(expected, Compatible(Version("A", "1.0", ksp, min, max), OnlyInstance));
    }

    [Fact]
    public void Two_part_instance_version_means_x_y_0()
    {
        var criteria = new GameVersionCriteria("1.12", []);

        Assert.Equal("1.12.0", criteria.InstanceVersion);
        Assert.True(Compatible(Version("A", "1.0", "1.12.0"), criteria));
    }

    [Fact]
    public void Criteria_splits_full_versions_and_prefixes()
    {
        var criteria = new GameVersionCriteria("1.12", ["1.11", "1.12.3", "1.10.1", " "]);

        Assert.Equal(new[] { "1.12.0", "1.12.3", "1.10.1" }, criteria.ExactVersions.ToArray());
        Assert.Equal(new[] { "1.11" }, criteria.MinorPrefixes.ToArray());
        Assert.Equal("1.12.0, 1.12.3, 1.10.1, 1.11.x", criteria.ToString());
    }

    [Fact]
    public void Mod_for_older_version_becomes_compatible_once_listed()
    {
        var modFor111 = Version("A", "1.0", "1.11");

        Assert.False(Compatible(modFor111, OnlyInstance));
        Assert.True(Compatible(modFor111, new GameVersionCriteria("1.12.5", ["1.11"])));
        Assert.True(Compatible(Version("A", "1.0", null, "1.9", "1.11"), new GameVersionCriteria("1.12.5", ["1.11"])));
    }

    [Fact]
    public void Strict_mod_needs_an_exactly_listed_version()
    {
        var strict = Version("A", "1.0", "1.12.3", strict: true);

        Assert.False(Compatible(strict, new GameVersionCriteria("1.12.5", ["1.12"])));
        Assert.True(Compatible(strict, new GameVersionCriteria("1.12.5", ["1.12.3"])));
        Assert.False(Compatible(Version("B", "1.0", "1.11.2", strict: true), new GameVersionCriteria("1.12.5", ["1.11"])));
    }

    [Fact]
    public void Strict_mod_targeting_a_minor_release_matches_its_patches()
    {
        Assert.True(Compatible(Version("A", "1.0", "1.12", strict: true), OnlyInstance));
    }

    [Fact]
    public void No_criteria_means_no_filter()
    {
        Assert.True(CompatibilityService.IsCompatible(Version("A", "1.0", "0.90"), null));
    }

    [Fact]
    public void Lookups_return_newest_compatible_version()
    {
        var mod = Mod(Version("A", "3.0", "1.12"), Version("A", "2.0", "1.11"), Version("A", "1.0", "1.10"));
        var provider = Mod(Version("P", "5.0", "1.12", provides: ["Virtual"]), Version("P", "4.0", "1.11", provides: ["Virtual"]));
        var oldProvider = Mod(Version("Q", "1.0", "1.9", provides: ["Virtual"]));
        var compatibility = new TestServices(mod, provider, oldProvider).Compatibility;
        var ksp111 = new GameVersionCriteria("1.11.2", []);

        Assert.Equal("2.0", compatibility.GetCompatibleVersionFromRelation(Rel("A"), ksp111).Version);
        Assert.Equal("3.0", compatibility.GetCompatibleVersionFromRelation(Rel("A"), null).Version);

        var providers = compatibility.GetVersionsFromModule("Virtual", ksp111);
        Assert.Equal("P-4.0", Assert.Single(providers).Id);
        Assert.Equal(2, compatibility.GetVersionsFromModule("Virtual").Count);
    }
}
