using KMRLauncherMvvm.Services;
using static KMRLauncherMvvm.Tests.TestData;

namespace KMRLauncherMvvm.Tests;

// Spec "conflicts"
public class ConflictTests
{
    private static readonly KMRLauncherMvvm.Models.ModVersion A = Version("A", "1.0", conflicts: [Rel("B", max: "1.5")]);
    private static readonly KMRLauncherMvvm.Models.ModVersion B1 = Version("B", "1.0");
    private static readonly KMRLauncherMvvm.Models.ModVersion B2 = Version("B", "2.0");

    [Fact]
    public void New_mods_conflicting_with_each_other()
    {
        var problem = Assert.Single(CompatibilityService.FindConflicts([A, B1], []));

        Assert.True(problem.IsConflict);
        Assert.Contains("can't be installed together", problem.Message);
    }

    [Fact]
    public void Version_outside_conflict_limits_is_fine()
    {
        Assert.Empty(CompatibilityService.FindConflicts([A, B2], []));
    }

    [Fact]
    public void Both_directions_against_installed_mods()
    {
        Assert.Contains("already installed", Assert.Single(CompatibilityService.FindConflicts([A], [B1])).Message);
        Assert.Single(CompatibilityService.FindConflicts([B1], [A]));
    }

    [Fact]
    public void Virtual_module_conflicts_block_a_second_provider_only()
    {
        var engine1 = Version("Engine1", "1.0", conflicts: [Rel("Engine")], provides: ["Engine"]);
        var engine2 = Version("Engine2", "1.0", conflicts: [Rel("Engine")], provides: ["Engine"]);

        Assert.Empty(CompatibilityService.FindConflicts([engine1], []));
        Assert.Single(CompatibilityService.FindConflicts([engine1, engine2], []));
    }

    [Fact]
    public void Installed_mod_being_replaced_is_not_checked()
    {
        var services = new TestServices(Mod(A), Mod(B2, B1));
        var data = new KMRLauncherMvvm.Models.InstallModsData
        {
            SelectedInstance = Instance("1.12.5", A, B1),
            RequestedModVersions = [Requested(B2)]
        };

        services.Dependency.ResolveDependencies(data);

        Assert.Empty(data.Problems);
    }
}
