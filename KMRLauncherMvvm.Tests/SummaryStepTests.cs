using KMRLauncherMvvm.Models;
using KMRLauncherMvvm.ViewModels.InstallMods;
using static KMRLauncherMvvm.Tests.TestData;

namespace KMRLauncherMvvm.Tests;

// The summary step reads the final InstallModsData; build it with the resolver and the Modules step like the wizard
public class SummaryStepTests
{
    private static (InstallModsSummaryStepViewModel summary, InstallModsData data) Summarize(TestServices services,
        Instance instance, params ModVersion[] requested)
    {
        var data = new InstallModsData
        {
            SelectedInstance = instance,
            RequestedModVersions = [.. requested.Select(Requested)]
        };
        services.Dependency.ResolveDependencies(data);
        new InstallModsModulesStepViewModel(data, services.Compatibility, services.Dependency).Prepare();

        var summary = new InstallModsSummaryStepViewModel(data);
        summary.Prepare();
        return (summary, data);
    }

    private static ModVersion Sized(ModVersion version, int? size)
    {
        version.DownloadSize = size;
        return version;
    }

    [Fact]
    public void Lists_mods_with_reason_and_totals()
    {
        var host = Sized(Version("Host", "1.0", depends: [Rel("Lib")]), 1024 * 1024);
        var lib = Sized(Version("Lib", "2.0"), null);
        var services = new TestServices(Mod(host), Mod(lib));

        var (summary, _) = Summarize(services, Instance(), host);

        Assert.Equal(new[] { "Host", "Lib" }, summary.ToInstall.Select(r => r.Identifier));
        Assert.Equal("Dependency of Host-1.0", summary.ToInstall.Single(r => r.Identifier == "Lib").Details);
        Assert.Equal("1 MB", summary.ToInstall.Single(r => r.Identifier == "Host").Size);
        Assert.Equal("2 mods to install, 1 MB to download (size unknown for 1 mod)", summary.TotalsText);
        Assert.True(summary.CanFinish);
        Assert.False(summary.HasRemovals);
    }

    [Fact]
    public void Replaced_installed_version_is_shown_on_the_install_row()
    {
        var host = Version("Host", "1.0", depends: [Rel("Lib", min: "2.0")]);
        var services = new TestServices(Mod(host), Mod(Version("Lib", "2.0"), Version("Lib", "1.0")));

        var (summary, data) = Summarize(services, Instance("1.12.5", Version("Lib", "1.0")), host);

        Assert.Single(data.RequestedRemoveModVersions);
        Assert.EndsWith("replaces 1.0", summary.ToInstall.Single(r => r.Identifier == "Lib").Details);
        Assert.Empty(summary.ToRemove);
    }

    [Fact]
    public void Conflict_blocks_finish()
    {
        var host = Version("Host", "1.0", conflicts: [Rel("Installed")]);
        var services = new TestServices(Mod(host));

        var (summary, _) = Summarize(services, Instance("1.12.5", Version("Installed", "1.0")), host);

        Assert.True(summary.HasProblems);
        Assert.False(summary.CanFinish);
        Assert.False(summary.CanGoNext);
    }

    [Fact]
    public void Nothing_to_do_blocks_finish()
    {
        var host = Version("Host", "1.0");
        var services = new TestServices(Mod(host));

        var (summary, _) = Summarize(services, Instance("1.12.5", host), host);

        Assert.True(summary.HasNothingToDo);
        Assert.False(summary.CanFinish);
    }
}
