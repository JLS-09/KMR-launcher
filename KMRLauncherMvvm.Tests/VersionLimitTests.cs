using KMRLauncherMvvm.Exceptions;
using KMRLauncherMvvm.Models;
using KMRLauncherMvvm.Services;
using static KMRLauncherMvvm.Tests.TestData;

namespace KMRLauncherMvvm.Tests;

public class VersionLimitTests
{
    private static readonly Mod ModA = Mod(Version("A", "1.3"), Version("A", "1.2.1"), Version("A", "1.1"));
    private readonly CompatibilityService _compatibility = new TestServices(ModA).Compatibility;

    [Theory]
    [InlineData(null, "1.2", null, "1.3")] // min that isn't released
    [InlineData(null, null, "1.2.5", "1.2.1")] // max that isn't released
    [InlineData(null, "1.0", "1.2", "1.1")]
    [InlineData("1.2.1", null, null, "1.2.1")]
    [InlineData(null, null, null, "1.3")]
    public void Picks_newest_version_within_limits(string? exact, string? min, string? max, string expected)
    {
        var version = _compatibility.GetCompatibleVersionFromRelation(Rel("A", exact, min, max), null);

        Assert.Equal(expected, version.Version);
    }

    [Fact]
    public void No_version_within_limits_throws()
    {
        Assert.Throws<NoCompatibleVersionFoundException>(() =>
            _compatibility.GetCompatibleVersionFromRelation(Rel("A", min: "2.0"), null));
    }

    [Fact]
    public void Mixing_version_with_min_is_invalid()
    {
        Assert.Throws<InvalidRelationshipException>(() =>
            _compatibility.GetCompatibleVersionFromRelation(Rel("A", "1.1", min: "1.0"), null));
    }

    [Fact]
    public void Version_check_uses_identifier_and_limits()
    {
        Assert.True(_compatibility.IsVersionCompatibleWithRelation(ModA.Versions[1], Rel("A", min: "1.2")));
        Assert.False(_compatibility.IsVersionCompatibleWithRelation(ModA.Versions[2], Rel("A", min: "1.2")));
        Assert.False(_compatibility.IsVersionCompatibleWithRelation(ModA.Versions[0], Rel("B")));
    }

    [Fact]
    public void Combine_takes_the_stricter_limits()
    {
        var combined = _compatibility.CombineRelationships(Rel("A", min: "1.0", max: "1.5"), Rel("A", min: "1.2", max: "1.4"));

        Assert.Equal("1.2", combined.MinVersion);
        Assert.Equal("1.4", combined.MaxVersion);
    }

    [Fact]
    public void Combine_with_exact_version_inside_range()
    {
        var combined = _compatibility.CombineRelationships(Rel("A", min: "1.2"), Rel("A", "1.3"));

        Assert.Equal("1.3", combined.Version);
        Assert.Null(combined.MinVersion);
    }

    [Fact]
    public void Combine_works_with_unreleased_limits()
    {
        var combined = _compatibility.CombineRelationships(Rel("A", min: "1.15"), Rel("A", max: "1.99"));

        Assert.Equal("1.15", combined.MinVersion);
        Assert.Equal("1.99", combined.MaxVersion);
    }

    [Fact]
    public void Combine_of_excluding_limits_throws()
    {
        Assert.Throws<NoCompatibleVersionFoundException>(() =>
            _compatibility.CombineRelationships(Rel("A", min: "1.4"), Rel("A", max: "1.2")));
        Assert.Throws<NoCompatibleVersionFoundException>(() =>
            _compatibility.CombineRelationships(Rel("A", min: "1.4"), Rel("A", "1.3")));
    }

    [Fact]
    public void Combine_of_virtual_name_drops_limits()
    {
        // Decided: limits on virtual names are ignored
        var combined = _compatibility.CombineRelationships(Rel("Virtual", min: "1.0"), Rel("Virtual", max: "0.5"));

        Assert.Null(combined.MinVersion);
        Assert.Null(combined.MaxVersion);
    }
}
