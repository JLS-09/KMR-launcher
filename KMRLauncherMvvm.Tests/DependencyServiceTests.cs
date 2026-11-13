using KMRLauncherMvvm.Models;
using static KMRLauncherMvvm.Tests.TestData;

namespace KMRLauncherMvvm.Tests;

public class DependencyServiceTests
{
    private static InstallModsData Resolve(TestServices services, Instance instance, params PlannedModVersion[] requested)
    {
        var data = new InstallModsData { SelectedInstance = instance, RequestedModVersions = [.. requested] };
        services.Dependency.ResolveDependencies(data);
        return data;
    }

    [Fact]
    public void Broken_dependencies_become_problems_and_the_rest_still_resolves()
    {
        var main = Version("Main", "1.0", depends:
        [
            Rel("Old"), // only a KSP 1.9 version exists
            Rel("Good"),
            Rel("Lib", "1.0", min: "0.5"), // invalid metadata
            Rel("Lib", min: "3.0") // no such version
        ]);
        var services = new TestServices(Mod(main), Mod(Version("Old", "1.0", "1.9")), Mod(Version("Good", "1.0")),
            Mod(Version("Lib", "2.0"), Version("Lib", "1.0")));

        var data = Resolve(services, Instance(), Requested(main));

        Assert.Equal(3, data.Problems.Count);
        Assert.Contains(data.Problems, p => p.Message.Contains("supports KSP"));
        Assert.Contains(data.Problems, p => p.Message.Contains("invalid metadata"));
        Assert.Contains(data.Problems, p => p.Message.Contains("no such version"));
        Assert.Contains(data.FinalModList, m => m.Version.Identifier == "Good");
    }

    [Fact]
    public void Requested_version_that_does_not_fit_the_instance_is_a_problem()
    {
        var old = Version("Main", "0.9", "1.8");
        var data = Resolve(new TestServices(Mod(old)), Instance(), Requested(old));

        Assert.Contains("doesn't support KSP", Assert.Single(data.Problems).Message);
    }

    [Fact]
    public void User_picked_version_is_never_swapped()
    {
        var lib2 = Version("Lib", "2.0");
        var other = Version("Other", "1.0", depends: [Rel("Lib", max: "1.0")]);
        var services = new TestServices(Mod(lib2, Version("Lib", "1.0")), Mod(other));

        var data = Resolve(services, Instance(), Requested(lib2), Requested(other));

        var lib = Assert.Single(data.FinalModList, m => m.Version.Identifier == "Lib");
        Assert.Equal("2.0", lib.Version.Version);
        Assert.Contains("You picked Lib 2.0", Assert.Single(data.Problems).Message);
    }

    [Fact]
    public void Swapped_dependency_must_fit_every_mod_that_needs_it()
    {
        var lib = Mod(Version("Lib", "2.0"), Version("Lib", "1.0"));
        var needsLow = Version("NeedsLow", "1.0", depends: [Rel("Lib", min: "0.5")]);
        var needsMax1 = Version("NeedsMax1", "1.0", depends: [Rel("Lib", max: "1.0")]);
        var needsMin2 = Version("NeedsMin2", "1.0", depends: [Rel("Lib", min: "2.0")]);
        var services = new TestServices(lib, Mod(needsLow), Mod(needsMax1), Mod(needsMin2));

        // >= 0.5 picks 2.0, then <= 1.0 swaps to 1.0, which fits both
        var fits = Resolve(services, Instance(), Requested(needsLow), Requested(needsMax1));
        var scheduled = Assert.Single(fits.FinalModList, m => m.Version.Identifier == "Lib");
        Assert.Empty(fits.Problems);
        Assert.Equal("1.0", scheduled.Version.Version);
        Assert.Equal(2, scheduled.RequiredBy.Count);

        // <= 1.0 and >= 2.0 can't both be met
        var clash = Resolve(services, Instance(), Requested(needsMax1), Requested(needsMin2));
        Assert.Contains("needs Lib version 1.0 or older", Assert.Single(clash.Problems).Message);
    }

    [Fact]
    public void Any_of_goes_to_the_choices()
    {
        var host = Version("Host", "1.0", depends: [AnyOf(Entry("X"), Entry("Y"))]);
        var data = Resolve(new TestServices(Mod(host), Mod(Version("X", "1.0")), Mod(Version("Y", "1.0"))), Instance(),
            Requested(host));

        Assert.Single(data.ChoosableVersions);
        Assert.Single(data.FinalModList);
    }

    [Fact]
    public void Installed_provider_satisfies_a_real_mod_dependency()
    {
        var fork = Version("MMFork", "1.0", provides: ["ModuleManager"]);
        var needsMm = Version("NeedsMM", "1.0", depends: [Rel("ModuleManager")]);
        var services = new TestServices(Mod(Version("ModuleManager", "4.0")), Mod(needsMm), Mod(fork));

        var data = Resolve(services, Instance("1.12.5", fork), Requested(needsMm));

        Assert.Single(data.FinalModList);
        Assert.Empty(data.Problems);
    }

    [Fact]
    public void Queued_provider_satisfies_a_real_mod_dependency()
    {
        var fork = Version("MMFork", "1.0", provides: ["ModuleManager"]);
        var needsMm = Version("NeedsMM", "1.0", depends: [Rel("ModuleManager")]);
        var services = new TestServices(Mod(Version("ModuleManager", "4.0")), Mod(needsMm), Mod(fork));

        var data = Resolve(services, Instance(), Requested(fork), Requested(needsMm));

        Assert.Equal(2, data.FinalModList.Count);
        Assert.Contains("NeedsMM-1.0", data.FinalModList.Single(m => m.Version.Identifier == "MMFork").RequiredBy);
    }
}
