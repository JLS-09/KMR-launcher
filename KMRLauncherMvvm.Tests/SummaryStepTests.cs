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

        Assert.Equal(new[] { "Host", "Lib" }, summary.Rows.Select(r => r.Name));
        Assert.All(summary.Rows, r => Assert.Equal(ChangeKind.New, r.Kind));
        Assert.Equal("Picked by you", summary.Rows.Single(r => r.Name == "Host").Reason);
        Assert.Equal("Dependency of Host", summary.Rows.Single(r => r.Name == "Lib").Reason);
        Assert.Equal("1.0 MB", summary.Rows.Single(r => r.Name == "Host").SizeText);
        Assert.Equal("2", summary.NewText);
        Assert.Equal("1.0 MB", summary.ToDownloadText);
        Assert.Equal("Install 2 mods", summary.InstallText);
        Assert.True(summary.CanFinish);
    }

    [Fact]
    public void Replaced_installed_version_is_an_update_row()
    {
        var host = Version("Host", "1.0", depends: [Rel("Lib", min: "2.0")]);
        var services = new TestServices(Mod(host), Mod(Version("Lib", "2.0"), Version("Lib", "1.0")));

        var (summary, data) = Summarize(services, Instance("1.12.5", Version("Lib", "1.0")), host);

        Assert.Single(data.RequestedRemoveModVersions);
        var lib = summary.Rows.Single(r => r.Name == "Lib");
        Assert.Equal(ChangeKind.Update, lib.Kind);
        Assert.Equal("replaces 1.0", lib.ReplacesText);
        Assert.Equal("1.0 → 2.0", lib.ChangeText);
        Assert.DoesNotContain(summary.Rows, r => r.IsRemove);
        Assert.Equal("1", summary.UpdatesText);
    }

    [Fact]
    public void Cached_downloads_are_marked_and_not_counted_to_download()
    {
        var host = Sized(Version("Host", "1.0"), 3 * 1024 * 1024);
        var services = new TestServices(Mod(host));
        var data = new InstallModsData { SelectedInstance = Instance(), RequestedModVersions = [Requested(host)] };
        services.Dependency.ResolveDependencies(data);

        var summary = new InstallModsSummaryStepViewModel(data) { IsCached = _ => true };
        summary.Prepare();

        Assert.True(summary.Rows.Single().ShowCached);
        Assert.Equal("", summary.Rows.Single().SizeText);
        Assert.Equal("0.0 MB", summary.ToDownloadText);
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
