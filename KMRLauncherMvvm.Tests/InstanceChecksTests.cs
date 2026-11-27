using KMRLauncherMvvm.Models;
using KMRLauncherMvvm.Services;
using static KMRLauncherMvvm.Tests.TestData;

namespace KMRLauncherMvvm.Tests;

public class InstanceChecksTests
{
    [Fact]
    public void Every_installed_mod_is_checked_for_updates_issues_and_replacements()
    {
        var updatable = Version("A", "1.0");
        var notListed = Version("B", "1.0", ksp: "1.7");
        var replaced = Version("Old", "1.0");
        var final = Version("Old", "1.1");
        final.ReplacedBy = new Relationship { Name = "New" };
        var unknown = Version("Gone", "1.0");
        var instance = Instance("1.12.5", updatable, notListed, replaced, unknown);
        var mods = new[]
        {
            Mod(Version("A", "1.1"), updatable), Mod(notListed), Mod(final, replaced), Mod(Version("New", "2.0"))
        }.ToDictionary(m => m.Id);

        var checks = InstanceChecks.CheckAll(instance, mods).ToDictionary(c => c.Installed.Identifier);

        Assert.Equal("1.1", checks["A"].Update?.Version);
        Assert.False(checks["A"].IsNotListed);
        Assert.True(checks["B"].IsNotListed);
        Assert.False(checks["B"].HasUpdate);
        Assert.Equal("New", checks["Old"].Replacement?.Identifier);
        Assert.False(checks["Old"].HasUpdate);
        Assert.Null(checks["Gone"].Mod);
        Assert.False(checks["Gone"].HasUpdate);
    }

    [Fact]
    public void An_update_has_to_fit_the_instance()
    {
        var installed = Version("A", "1.0");
        var instance = Instance("1.12.5", installed);
        var mods = new[] { Mod(Version("A", "2.0", ksp: "1.7"), Version("A", "1.1"), installed) }.ToDictionary(m => m.Id);

        Assert.Equal("1.1", InstanceChecks.CheckAll(instance, mods).Single().Update?.Version);
    }
}
