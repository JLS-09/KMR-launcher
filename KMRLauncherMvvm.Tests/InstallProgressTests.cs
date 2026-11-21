using System.Collections.ObjectModel;
using KMRLauncherMvvm.Models;
using KMRLauncherMvvm.Services;
using KMRLauncherMvvm.ViewModels.InstallMods;
using static KMRLauncherMvvm.Tests.TestData;

namespace KMRLauncherMvvm.Tests;

public class InstallProgressTests
{
    private class NoDownloads : IModDownloader
    {
        public string? FindCached(ModVersion mod) => null;

        public Task<string> DownloadAsync(ModVersion mod, Action<long, long?>? onProgress,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private static InstallProgressViewModel Progress(params ModVersion[] mods)
    {
        var instance = Instance();
        var run = InstallRun.Create(instance, new InstallModsData
        {
            SelectedInstance = instance,
            FinalModList = new ObservableCollection<PlannedModVersion>(mods.Select(Requested))
        });
        return new InstallProgressViewModel(new InstallService(new NoDownloads(), _ => { }), run, 4);
    }

    private static ModInstallJob Job(InstallProgressViewModel progress, string identifier) =>
        progress.Rows.Single(r => r.Identifier == identifier).Job;

    [Fact]
    public void Starts_with_one_queued_row_per_mod()
    {
        var progress = Progress(Version("A", "1.0"), Version("B", "2.0"));

        Assert.Equal(["A", "B"], progress.Rows.Select(r => r.Identifier));
        Assert.All(progress.Rows, r => Assert.Equal("Queued", r.StateText));
        Assert.Equal(0, progress.OverallProgress);
        Assert.Equal("0 / 2 mods", progress.OverallText);
    }

    [Fact]
    public void Overall_progress_counts_downloads_three_quarters_and_installing_one_quarter()
    {
        var progress = Progress(Version("A", "1.0"), Version("B", "1.0"));
        var a = Job(progress, "A");
        a.State = ModInstallState.Downloading;
        a.BytesDone = 50;
        a.BytesTotal = 100;
        var b = Job(progress, "B");
        b.State = ModInstallState.Installing;
        b.BytesDone = 100;
        b.BytesTotal = 100;
        b.FilesDone = 1;
        b.FilesTotal = 4;

        progress.Refresh(TimeSpan.FromSeconds(1));

        // A: 0.75 * 0.5 = 0.375, B: 0.75 + 0.25 * 0.25 = 0.8125
        Assert.Equal((0.375 + 0.8125) / 2, progress.OverallProgress, 6);
        Assert.Equal("59%", progress.PercentText);
        Assert.Equal("0 / 2 mods · 150 B / 200 B", progress.OverallText);
        Assert.Equal("50 / 100 B", progress.Rows[0].DetailText);
        Assert.Equal("150 / 200 B", progress.BytesText);
        Assert.Equal("1 / 4 files", progress.Rows[1].DetailText);
        Assert.Equal(0.25, progress.Rows[1].Progress);
    }

    [Fact]
    public void Row_texts_follow_the_job()
    {
        var meta = Version("Meta", "1.0");
        meta.Kind = "metapackage";
        var progress = Progress(Version("Cached", "1.0"), Version("Unknown", "1.0"), Version("Broken", "1.0"), meta);

        var cached = Job(progress, "Cached");
        cached.IsCached = true;
        cached.State = ModInstallState.Done;
        cached.FilesTotal = 12;
        Job(progress, "Unknown").State = ModInstallState.Downloading;
        var broken = Job(progress, "Broken");
        broken.State = ModInstallState.Failed;
        broken.Error = "Couldn't download Broken 1.0.";
        Job(progress, "Meta").State = ModInstallState.Done;

        progress.Refresh(TimeSpan.FromSeconds(1));

        var rows = progress.Rows.ToDictionary(r => r.Identifier);
        Assert.Equal("Cached · 12 files", rows["Cached"].DetailText);
        Assert.True(rows["Unknown"].IsIndeterminate);
        Assert.Equal("Failed", rows["Broken"].StateText);
        Assert.True(rows["Broken"].IsFailed);
        Assert.Equal("Couldn't download Broken 1.0.", rows["Broken"].DetailText);
        Assert.Equal("Nothing to download", rows["Meta"].DetailText);
        Assert.Equal("2 / 4 mods", progress.OverallText);
    }
}