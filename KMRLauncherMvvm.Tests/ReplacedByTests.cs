using System.Diagnostics;
using KMRLauncherMvvm.Models;
using KMRLauncherMvvm.Services;
using KMRLauncherMvvm.ViewModels.InstallMods;
using static KMRLauncherMvvm.Tests.TestData;

namespace KMRLauncherMvvm.Tests;

public class ReplacedByTests
{
    private static ModVersion Final(string version, string by, string? exact = null, string? min = null)
    {
        var final = Version("Old", version);
        final.ReplacedBy = new Relationship { Name = by, Version = exact, MinVersion = min };
        return final;
    }

    private static Dictionary<string, Mod> Mods(params Mod[] mods) => mods.ToDictionary(m => m.Id);

    [Fact]
    public void Newest_release_names_the_replacement()
    {
        var installed = Version("Old", "1.0");
        var instance = Instance("1.12.5", installed);
        var mods = Mods(Mod(Final("1.1", "New"), installed), Mod(Version("New", "2.0"), Version("New", "1.0")));

        var replacement = InstalledMods.FindReplacement(instance, installed, mods, GameVersionCriteria.FromInstance(instance));

        Assert.Equal("New", replacement?.Identifier);
        Assert.Equal("2.0", replacement?.Version);
    }

    [Fact]
    public void Version_limits_pick_the_replacement_version()
    {
        var installed = Version("Old", "1.0");
        var instance = Instance("1.12.5", installed);
        var criteria = GameVersionCriteria.FromInstance(instance);
        var newMod = Mod(Version("New", "2.0"), Version("New", "1.5"), Version("New", "1.0"));

        Assert.Equal("1.5", InstalledMods.FindReplacement(instance, installed,
            Mods(Mod(Final("1.1", "New", exact: "1.5")), newMod), criteria)?.Version);
        Assert.Null(InstalledMods.FindReplacement(instance, installed,
            Mods(Mod(Final("1.1", "New", min: "3.0")), newMod), criteria));
    }

    [Fact]
    public void No_replacement_when_it_doesnt_fit_or_is_installed_or_the_mod_came_back()
    {
        var installed = Version("Old", "1.0");
        var instance = Instance("1.12.5", installed);
        var criteria = GameVersionCriteria.FromInstance(instance);
        var notFitting = Mod(Version("New", "2.0", ksp: "1.8.1", strict: true));

        Assert.Null(InstalledMods.FindReplacement(instance, installed, Mods(Mod(Final("1.1", "New")), notFitting), criteria));

        var both = Instance("1.12.5", installed, Version("New", "2.0"));
        Assert.Null(InstalledMods.FindReplacement(both, installed,
            Mods(Mod(Final("1.1", "New")), Mod(Version("New", "2.0"))), criteria));

        Assert.Null(InstalledMods.FindReplacement(instance, installed,
            Mods(Mod(Version("Old", "2:1.0"), Final("1.1", "New")), Mod(Version("New", "2.0"))), criteria));
    }

    [Fact]
    public void Replacing_installs_the_new_mod_and_removes_the_old_one()
    {
        var old = Version("Old", "1.0");
        var user = Version("User", "1.0", depends: [Rel("Old")]);
        var replacement = Version("New", "2.0", provides: ["Old"], conflicts: [Rel("Old")]);
        var services = new TestServices(Mod(Final("1.1", "New"), old), Mod(user), Mod(replacement));
        var data = new InstallModsData
        {
            SelectedInstance = Instance("1.12.5", old, user),
            RequestedModVersions =
            [
                new PlannedModVersion
                {
                    Version = replacement, IsUserRequested = true, IsVersionPickedByUser = true, Replaces = "Old"
                }
            ],
            ReplacedModVersions = [old]
        };

        services.Dependency.ResolveDependencies(data);

        Assert.Empty(data.Problems);
        Assert.Equal(["New"], data.FinalModList.Select(p => p.Version.Identifier));
        Assert.Equal(["Old"], data.RequestedRemoveModVersions.Select(p => p.Version.Identifier));

        var rows = InstallChanges.Rows(data, _ => false);
        Assert.Equal("Replaces Old", rows.Single(r => r.Name == "New").Reason);
        Assert.Equal("Replaced by New", rows.Single(r => r.Name == "Old").Reason);
    }

    [Fact]
    public void A_mod_being_removed_doesnt_satisfy_a_dependency()
    {
        var old = Version("Old", "1.0");
        var replacement = Version("New", "2.0", depends: [Rel("Old")]);
        var services = new TestServices(Mod(old), Mod(replacement));
        var data = new InstallModsData
        {
            SelectedInstance = Instance("1.12.5", old),
            RequestedModVersions = [Requested(replacement)],
            ReplacedModVersions = [old]
        };

        services.Dependency.ResolveDependencies(data);

        Assert.Contains(data.FinalModList, p => p.Version.Identifier == "Old");
    }

    [Fact]
    public void Stop_ends_the_whole_process_tree()
    {
        var psi = new ProcessStartInfo("sh") { UseShellExecute = false };
        psi.ArgumentList.Add("-c");
        psi.ArgumentList.Add("sleep 30 & wait");
        using var process = Process.Start(psi)!;

        WineLauncher.Stop(process, "/nonexistent-prefix");

        Assert.True(process.WaitForExit(5000));
    }
}
