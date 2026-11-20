using KMRLauncherMvvm.Models;
using KMRLauncherMvvm.Services;
using static KMRLauncherMvvm.Tests.TestData;

namespace KMRLauncherMvvm.Tests;

// Spec "depends" with version limits, checked for the installed mods that stay: an update must not
// break what an installed mod needs.
public class BrokenDependsTests
{
    private static readonly ModVersion Lib1 = Version("Lib", "1.0");
    private static readonly ModVersion Lib2 = Version("Lib", "2.0");
    private static readonly ModVersion UsesLib1 = Version("A", "1.0", depends: [Rel("Lib", max: "1.9")]);

    [Fact]
    public void Updating_past_an_installed_mods_limit_is_a_problem()
    {
        var problem = Assert.Single(CompatibilityService.FindBrokenDepends([Lib2], [UsesLib1], [UsesLib1, Lib1]));

        Assert.Equal("A", problem.Mod);
        Assert.Contains("needs Lib version 1.9 or older", problem.Message);
        Assert.Contains("installs Lib 2.0", problem.Message);
        Assert.False(problem.IsConflict);
    }

    [Fact]
    public void Update_within_the_limit_is_fine()
    {
        var lib15 = Version("Lib", "1.5");

        Assert.Empty(CompatibilityService.FindBrokenDepends([lib15], [UsesLib1], [UsesLib1, Lib1]));
    }

    [Fact]
    public void A_depend_that_was_already_unmet_isnt_reported()
    {
        var other = Version("Other", "1.0");

        Assert.Empty(CompatibilityService.FindBrokenDepends([other], [UsesLib1], [UsesLib1]));
    }

    [Fact]
    public void Removing_what_an_installed_mod_needs_is_a_problem()
    {
        var problem = Assert.Single(CompatibilityService.FindBrokenDepends([], [UsesLib1], [UsesLib1, Lib1]));

        Assert.Contains("which this change removes", problem.Message);
    }

    [Fact]
    public void Limits_are_ignored_for_providers()
    {
        var needsVirtual = Version("A", "1.0", depends: [Rel("Engine", max: "1.0")]);
        var provider1 = Version("Engine1", "1.0", provides: ["Engine"]);
        var provider2 = Version("Engine1", "5.0", provides: ["Engine"]);

        Assert.Empty(CompatibilityService.FindBrokenDepends([provider2], [needsVirtual], [needsVirtual, provider1]));
    }

    [Fact]
    public void Any_of_is_met_by_any_option_in_its_limits()
    {
        var needsEither = Version("A", "1.0", depends: [AnyOf(Entry("Lib", max: "1.9"), Entry("LibFork"))]);
        var fork = Version("LibFork", "1.0");

        Assert.Empty(CompatibilityService.FindBrokenDepends([Lib2], [needsEither, fork], [needsEither, Lib1, fork]));
        Assert.Contains("one of Lib, LibFork",
            Assert.Single(CompatibilityService.FindBrokenDepends([Lib2], [needsEither], [needsEither, Lib1])).Message);
    }

    [Fact]
    public void Resolver_reports_it_when_updating()
    {
        var services = new TestServices(Mod(Lib2, Lib1), Mod(UsesLib1));
        var data = new InstallModsData
        {
            SelectedInstance = Instance("1.12.5", UsesLib1, Lib1),
            RequestedModVersions = [new PlannedModVersion { Version = Lib2, IsVersionPickedByUser = true }]
        };

        services.Dependency.ResolveDependencies(data);

        Assert.Contains(data.Problems, p => p.Mod == "A" && p.Message.Contains("installs Lib 2.0"));
    }
}
