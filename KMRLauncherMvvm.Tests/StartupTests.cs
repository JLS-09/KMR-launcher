using System.Collections.ObjectModel;
using KMRLauncherMvvm.Models;
using KMRLauncherMvvm.Services;
using KMRLauncherMvvm.Services.Api;
using KMRLauncherMvvm.ViewModels.Startup;
using static KMRLauncherMvvm.Tests.TestData;

namespace KMRLauncherMvvm.Tests;

public class StartupTests : TempFolderTest
{
    private sealed class FakeApi(ModListService modList, Mod[] mods) : IModApiService
    {
        public bool HasCache { get; set; } = true;
        public bool FailFetch { get; set; }
        public bool WaitForSkip { get; set; }
        public List<bool> Calls { get; } = [];

        public async Task GetAllModsAsync(IProgress<ModFetchProgress>? progress, bool isRefresh,
            bool cacheOnly = false, CancellationToken cancel = default)
        {
            Calls.Add(cacheOnly);
            if (!cacheOnly && FailFetch) throw new InvalidOperationException("Couldn't reach github.com");
            if (!cacheOnly && WaitForSkip)
                while (!cancel.IsCancellationRequested) await Task.Delay(10);
            modList.Mods = new ObservableCollection<Mod>(mods);
        }
    }

    private readonly ModListService _modList = new();
    private InstanceService Instances { get; }
    private SyncStatusService Sync { get; }
    private HomeDataService HomeData { get; }
    private DownloadCache Cache => new(Path.Combine(Temp, "cache"), v => Path.Combine(Temp, "cache", v.Id + ".zip"));
    private string HomeFile => Path.Combine(Temp, "home.json");

    public StartupTests()
    {
        Directory.CreateDirectory(Temp);
        Instances = new InstanceService(Path.Combine(Temp, "settings.json"));
        Sync = new SyncStatusService(Path.Combine(Temp, "sync.json"));
        HomeData = new HomeDataService(HomeFile, Instances, _modList, Sync, Cache);
    }

    private (StartupViewModel Startup, FakeApi Api) Startup()
    {
        var installed = Version("A", "1.0");
        var instance = Instance("1.12.5", installed, Version("B", "1.0", ksp: "1.7"));
        Instances.Instances.Add(instance);
        var api = new FakeApi(_modList, [Mod(Version("A", "1.1"), installed), Mod(Version("B", "1.0", ksp: "1.7"))]);
        return (new StartupViewModel(Instances, api, _modList, HomeData), api);
    }

    [Fact]
    public async Task All_steps_run_and_the_checks_are_saved()
    {
        var (startup, api) = Startup();

        await startup.RunAsync();

        Assert.Equal([false], api.Calls);
        Assert.Equal("1 instance", startup.Settings.Detail);
        Assert.True(startup.Fetch.IsDone);
        Assert.Equal("Up to date", startup.Fetch.Detail);
        Assert.Equal("2 mods", startup.Read.Detail);
        Assert.Equal("1 update", startup.Check.Detail);
        Assert.Equal(1, startup.Progress);

        var saved = new HomeDataService(HomeFile, Instances, _modList, Sync, Cache).Data.Instances.Single();
        Assert.Equal("/tmp/test", saved.Folder);
        Assert.Equal(new ModUpdateResult("A", "1.0", "1.1"), saved.Updates.Single());
        Assert.Equal(["B"], saved.NotListed);
    }

    [Fact]
    public async Task Without_fetch_on_startup_the_cached_list_is_used()
    {
        var (startup, api) = Startup();
        Instances.Settings.FetchOnStartup = false;

        await startup.RunAsync();

        Assert.Equal([true], api.Calls);
        Assert.True(startup.Fetch.IsSkipped);
        Assert.Equal("Skipped", startup.Fetch.Detail);
        Assert.True(startup.Check.IsDone);
    }

    [Fact]
    public async Task A_failed_fetch_falls_back_to_the_cache()
    {
        var (startup, api) = Startup();
        api.FailFetch = true;

        await startup.RunAsync();

        Assert.Equal([false, true], api.Calls);
        Assert.True(startup.Fetch.IsFailed);
        Assert.True(startup.Read.IsDone);
        Assert.Equal("1 update", startup.Check.Detail);
    }

    [Fact]
    public async Task Without_any_mod_list_the_checks_are_skipped()
    {
        var (startup, api) = Startup();
        api.FailFetch = true;
        api.HasCache = false;

        await startup.RunAsync();

        Assert.Equal([false], api.Calls);
        Assert.False(startup.CanSkip);
        Assert.True(startup.Read.IsFailed);
        Assert.True(startup.Check.IsSkipped);
        Assert.Empty(HomeData.Data.Instances);
    }

    [Fact]
    public async Task Skip_stops_the_fetch()
    {
        var (startup, api) = Startup();
        api.WaitForSkip = true;

        var run = startup.RunAsync();
        Assert.True(startup.CanSkip);
        startup.SkipCommand.Execute(null);
        await run.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(startup.Fetch.IsSkipped);
        Assert.False(startup.CanSkip);
        Assert.True(startup.Check.IsDone);
    }

    [Fact]
    public void Checks_are_found_per_instance_by_folder()
    {
        var (_, _) = Startup();
        _modList.Mods = [Mod(Version("A", "1.1"), Version("A", "1.0"))];

        HomeData.Recheck();

        Assert.NotNull(HomeData.For(Instances.Instances.Single()));
        Assert.Null(HomeData.For(new Instance("Other", "/tmp/other", "1.12.5")));
    }

    [Fact]
    public void Changed_is_raised_even_without_a_mod_list()
    {
        var changed = 0;
        HomeData.Changed += () => changed++;

        HomeData.Recheck();

        Assert.Equal(1, changed);
        Assert.Empty(HomeData.Data.Instances);
    }

    [Fact]
    public void Unused_downloads_are_measured_and_cleaning_up_raises_changed()
    {
        var (_, _) = Startup();
        var cache = Cache;
        Directory.CreateDirectory(cache.Folder);
        File.WriteAllBytes(Path.Combine(cache.Folder, "A-1.0.zip"), new byte[10]);
        File.WriteAllBytes(Path.Combine(cache.Folder, "old.zip"), new byte[2048]);
        var changed = 0;
        cache.Changed += () => changed++;

        var data = HomeData.Recheck();
        cache.RemoveUnused(Instances.Instances);

        Assert.Equal((1, 2048L), (data.UnusedDownloads, data.UnusedDownloadBytes));
        Assert.Equal(1, changed);
        Assert.Equal(0, HomeData.Recheck().UnusedDownloads);
    }
}
