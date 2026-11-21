using KMRLauncherMvvm.Models;
using KMRLauncherMvvm.Services;
using KMRLauncherMvvm.ViewModels.Instances;
using static KMRLauncherMvvm.Tests.TestData;

namespace KMRLauncherMvvm.Tests;

public class InstalledModsTests
{
    // An instance with records: picked mods are user-requested, the rest are dependencies
    private static Instance With(ModVersion[] picked, ModVersion[] dependencies)
    {
        var instance = Instance("1.12.5", [.. picked, .. dependencies]);
        foreach (var mod in picked) instance.ModRecords[mod.Identifier] = new InstalledModRecord { IsUserRequested = true };
        foreach (var mod in dependencies) instance.ModRecords[mod.Identifier] = new InstalledModRecord();
        return instance;
    }

    private static string[] Ids(IEnumerable<ModVersion> mods) => [.. mods.Select(m => m.Identifier).Order()];

    [Fact]
    public void Removing_a_mod_removes_dependencies_nobody_else_needs()
    {
        var lib = Version("Lib", "1.0");
        var shared = Version("Shared", "1.0");
        var a = Version("A", "1.0", depends: [Rel("Lib"), Rel("Shared")]);
        var b = Version("B", "1.0", depends: [Rel("Shared")]);
        var instance = With([a, b], [lib, shared]);

        var plan = InstalledMods.PlanRemoval(instance, ["A"]);

        Assert.Equal(["A"], Ids(plan.Selected));
        Assert.Empty(plan.Dependents);
        Assert.Equal(["Lib"], Ids(plan.Orphans));
    }

    [Fact]
    public void Removing_a_dependency_removes_the_mods_that_need_it_and_their_unused_dependencies()
    {
        var lib = Version("Lib", "1.0");
        var other = Version("Other", "1.0");
        var a = Version("A", "1.0", depends: [Rel("Lib"), Rel("Other")]);
        var b = Version("B", "1.0", depends: [Rel("A")]);
        var instance = With([a, b], [lib, other]);

        var plan = InstalledMods.PlanRemoval(instance, ["Lib"]);

        Assert.Equal(["A", "B"], Ids(plan.Dependents));
        Assert.Equal(["Other"], Ids(plan.Orphans));
    }

    [Fact]
    public void A_dependency_stays_when_another_provider_or_any_of_option_is_left()
    {
        var provider1 = Version("Kopernicus", "1.0", provides: ["PlanetSystem"]);
        var provider2 = Version("KopernicusBE", "1.0", provides: ["PlanetSystem"]);
        var a = Version("A", "1.0", depends: [Rel("PlanetSystem")]);
        var b = Version("B", "1.0", depends: [AnyOf(Entry("Kopernicus"), Entry("KopernicusBE"))]);
        var instance = With([a, b, provider1, provider2], []);

        var plan = InstalledMods.PlanRemoval(instance, ["Kopernicus"]);

        Assert.Empty(plan.Dependents);
        Assert.Empty(plan.Orphans);
    }

    [Fact]
    public void A_dependency_that_was_already_missing_doesnt_remove_anything()
    {
        var a = Version("A", "1.0", depends: [Rel("NotInstalled"), Rel("B")]);
        var b = Version("B", "1.0");
        var c = Version("C", "1.0");
        var instance = With([a, b, c], []);

        var plan = InstalledMods.PlanRemoval(instance, ["C"]);
        Assert.Empty(plan.Dependents);

        Assert.Equal(["A"], Ids(InstalledMods.PlanRemoval(instance, ["B"]).Dependents));
    }

    [Fact]
    public void Picked_mods_and_mods_without_a_record_are_never_removed_as_unused()
    {
        var lib = Version("Lib", "1.0");
        var old = Version("Old", "1.0");
        var a = Version("A", "1.0", depends: [Rel("Lib"), Rel("Old")]);
        var instance = With([a, lib], []);
        instance.Mods.Add(old); // installed before records existed

        var plan = InstalledMods.PlanRemoval(instance, ["A"]);

        Assert.Empty(plan.Orphans);
    }

    [Fact]
    public void Removal_note_names_what_else_goes()
    {
        var lib = Version("KerbalKonstructs", "1.0");
        var tundra = Version("TundraSpaceCenter", "1.0", depends: [Rel("KerbalKonstructs")]);
        var instance = With([tundra], [lib]);

        Assert.Equal("Removing also removes KerbalKonstructs, which no other mod needs.",
            InstalledMods.DescribeRemoval(InstalledMods.PlanRemoval(instance, ["TundraSpaceCenter"])));
        Assert.Equal("Removing also removes TundraSpaceCenter, which depends on it.",
            InstalledMods.DescribeRemoval(InstalledMods.PlanRemoval(instance, ["KerbalKonstructs"])));

        var alone = With([Version("Solo", "1.0")], []);
        Assert.Equal("No other mods depend on the selection.",
            InstalledMods.DescribeRemoval(InstalledMods.PlanRemoval(alone, ["Solo"])));
    }

    [Fact]
    public void Reason_texts()
    {
        var lib = Version("Lib", "1.0");
        var unused = Version("Unused", "1.0");
        var dependents = Enumerable.Range(1, 6).Select(i => Version($"M{i}", "1.0", depends: [Rel("Lib")])).ToArray();
        var instance = With([.. dependents], [lib, unused]);
        instance.ModRecords["M1"].Reason = "Recommended by M2";

        Assert.Equal("Picked by you", InstalledMods.ReasonText(instance, dependents[1]));
        Assert.Equal("Recommended by M2", InstalledMods.ReasonText(instance, dependents[0]));
        Assert.Equal("Dependency of 6 mods", InstalledMods.ReasonText(instance, lib));
        Assert.Equal("Dependency, no mod needs it anymore", InstalledMods.ReasonText(instance, unused));

        var two = With([dependents[0], dependents[1]], [lib]);
        Assert.Equal("Dependency of M1 and 1 more", InstalledMods.ReasonText(two, lib));
    }

    [Theory]
    [InlineData("release-1.12.1-200", "release-1.12.1-201", "-201")]
    [InlineData("v2.4.1", "v2.4.2", "v2.4.2")]
    [InlineData("4.2.2", "4.2.3", "4.2.3")]
    public void Update_badge_shortens_long_versions(string installed, string next, string text) =>
        Assert.Equal(text, InstalledMods.ShortNextVersion(installed, next));

    [Theory]
    [InlineData(432_013_312L, "412 MB")]
    [InlineData(1_153_434L, "1.1 MB")]
    [InlineData(209_715L, "0.2 MB")]
    [InlineData(7_301_444_403L, "6.8 GB")]
    public void Sizes_are_formatted_like_the_mockups(long bytes, string text) =>
        Assert.Equal(text, InstalledMods.FormatSize(bytes));

    [Fact]
    public void Installed_row_shows_an_update_and_a_ksp_issue()
    {
        var installed = Version("A", "1.0");
        var newer = Version("A", "1.1");
        var old = Version("B", "1.0", ksp: "1.7");
        var instance = With([installed, old], []);
        var criteria = GameVersionCriteria.FromInstance(instance);

        var row = new InstalledModRowViewModel(instance, installed, Mod(newer, installed), criteria);
        var issue = new InstalledModRowViewModel(instance, old, Mod(old), criteria);

        Assert.True(row.HasUpdate);
        Assert.Equal("1.1", row.NextText);
        Assert.False(row.IsNotListed);
        Assert.True(issue.IsNotListed);
        Assert.Equal("Not listed for 1.12.5", issue.NotListedText);
    }

    [Fact]
    public void Compatible_chip_text()
    {
        var instance = Instance("1.12.5");
        instance.CompatibleKspVersions = ["1.12"];
        Assert.Equal("Compatible: 1.12.x", InstanceTexts.Compatible(instance));

        instance.CompatibleKspVersions = [];
        Assert.Equal("Compatible: 1.12.5 only", InstanceTexts.Compatible(instance));
    }
}
