using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.IO.Compression;
using KMRLauncherMvvm.Exceptions;
using KMRLauncherMvvm.Models;
using KMRLauncherMvvm.Services;
using static KMRLauncherMvvm.Tests.TestData;

namespace KMRLauncherMvvm.Tests;

public class InstallServiceTests : TempFolderTest
{
    private readonly string _root;
    private readonly FakeDownloader _downloader;
    private int _saves;

    public InstallServiceTests()
    {
        _root = Path.Combine(Temp, "instance");
        Directory.CreateDirectory(Path.Combine(_root, "GameData"));
        Directory.CreateDirectory(Path.Combine(_root, "Ships", "VAB"));
        _downloader = new FakeDownloader(Path.Combine(Temp, "downloads"));
    }

    // Serves zips built from a list of paths; a mod can be cached, slow (gate) or failing
    private class FakeDownloader(string folder) : IModDownloader
    {
        public Dictionary<string, string[]> Archives { get; } = [];
        public Dictionary<string, UnixFileMode> StoredModes { get; } = [];
        public HashSet<string> Cached { get; } = [];
        public HashSet<string> Failing { get; } = [];
        public ConcurrentDictionary<string, TaskCompletionSource> Gates { get; } = [];
        public ConcurrentBag<string> Downloaded { get; } = [];
        private int _active;
        public int MaxActive;

        public string? FindCached(ModVersion mod) => Cached.Contains(mod.Identifier) ? Write(mod) : null;

        public async Task<string> DownloadAsync(ModVersion mod, Action<long, long?>? onProgress,
            CancellationToken cancellationToken)
        {
            var active = Interlocked.Increment(ref _active);
            InterlockedMax(ref MaxActive, active);
            try
            {
                if (Gates.TryGetValue(mod.Identifier, out var gate))
                    await gate.Task.WaitAsync(cancellationToken);
                else
                    await Task.Delay(30, cancellationToken);

                if (Failing.Contains(mod.Identifier))
                    throw new ModInstallException($"Couldn't download {mod.Identifier} {mod.Version}. test.host: 404");

                onProgress?.Invoke(10, 10);
                Downloaded.Add(mod.Identifier);
                return Write(mod);
            }
            finally
            {
                Interlocked.Decrement(ref _active);
            }
        }

        private string Write(ModVersion mod)
        {
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, $"{mod.Identifier}-{mod.Version}.zip");
            if (File.Exists(path)) return path;
            using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
            foreach (var entry in Archives[mod.Identifier])
            {
                var zipEntry = zip.CreateEntry(entry);
                if (StoredModes.TryGetValue(mod.Identifier, out var mode))
                    zipEntry.ExternalAttributes = (0x8000 | (int)mode) << 16;
                using var writer = new StreamWriter(zipEntry.Open());
                writer.Write($"{mod.Identifier} {mod.Version}");
            }

            return path;
        }

        private static void InterlockedMax(ref int target, int value)
        {
            int current;
            while ((current = target) < value && Interlocked.CompareExchange(ref target, value, current) != current)
            {
            }
        }
    }

    private InstallService Service() => new(_downloader, _ => _saves++);

    private ModVersion Package(string identifier, string version = "1.0", params string[] files)
    {
        var mod = Version(identifier, version);
        mod.Download = [$"https://test.host/{identifier}.zip"];
        _downloader.Archives[identifier] =
            files.Length > 0 ? files : [$"{identifier}-{version}/GameData/{identifier}/{identifier}.cfg"];
        return mod;
    }

    private Instance Instance(params ModVersion[] installed)
    {
        var instance = TestData.Instance("1.12.5", installed);
        instance.RootPath = _root;
        return instance;
    }

    private static InstallRun Run(Instance instance, IEnumerable<PlannedModVersion> install,
        params ModVersion[] remove) =>
        InstallRun.Create(instance, new InstallModsData
        {
            SelectedInstance = instance,
            FinalModList = new ObservableCollection<PlannedModVersion>(install),
            RequestedRemoveModVersions =
                new ObservableCollection<PlannedModVersion>(remove.Select(r => new PlannedModVersion { Version = r }))
        });

    private static PlannedModVersion Dependency(ModVersion version) => new() { Version = version };

    private bool Exists(string relative) => File.Exists(Path.Combine(_root, relative));

    // An installed mod with its files on disk and a record
    private void Installed(Instance instance, ModVersion mod, params string[] files)
    {
        foreach (var file in files)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(_root, file))!);
            File.WriteAllText(Path.Combine(_root, file), "old");
        }

        instance.Mods.Add(mod);
        instance.ModRecords[mod.Identifier] = new InstalledModRecord { Files = [.. files], IsUserRequested = true };
    }

    [Fact]
    public async Task Installs_mods_records_their_files_and_saves()
    {
        var instance = Instance();
        var run = Run(instance, [Requested(Package("Foo")), Dependency(Package("Lib"))]);

        await Service().RunAsync(run, 4, CancellationToken.None);

        Assert.True(Exists("GameData/Foo/Foo.cfg"));
        Assert.True(Exists("GameData/Lib/Lib.cfg"));
        Assert.Equal(["Foo", "Lib"], instance.Mods.Select(m => m.Identifier).Order());
        Assert.Equal(["GameData/Foo/Foo.cfg"], instance.ModRecords["Foo"].Files);
        Assert.True(instance.ModRecords["Foo"].IsUserRequested);
        Assert.False(instance.ModRecords["Lib"].IsUserRequested);
        Assert.All(run.Jobs, j => Assert.Equal(ModInstallState.Done, j.State));
        Assert.Equal(1, _saves);
        Assert.False(Directory.Exists(Path.Combine(_root, ".kmr", "staging")));
        Assert.False(Directory.Exists(Path.Combine(_root, ".kmr-staging")));
    }

    [Fact]
    public async Task A_picked_mod_stays_picked_when_it_comes_back_as_a_dependency()
    {
        var instance = Instance();
        Installed(instance, Package("Lib"), "GameData/Lib/Lib.cfg");
        instance.ModRecords["Lib"].Reason = "Recommended by Foo";

        await Service().RunAsync(Run(instance, [Dependency(Package("Lib", "2.0"))]), 4, CancellationToken.None);

        Assert.True(instance.ModRecords["Lib"].IsUserRequested);
        Assert.Equal("Recommended by Foo", instance.ModRecords["Lib"].Reason);
    }

    [Fact]
    public async Task A_removal_without_installs_removes_the_files_and_records()
    {
        var instance = Instance();
        Installed(instance, Package("Foo"), "GameData/Foo/Foo.cfg");

        await Service().RunAsync(Run(instance, [], [instance.Mods[0]]), 4, CancellationToken.None);

        Assert.Empty(instance.Mods);
        Assert.Empty(instance.ModRecords);
        Assert.False(Exists("GameData/Foo/Foo.cfg"));
    }

    [Fact]
    public async Task Cached_mods_are_not_downloaded_again()
    {
        var instance = Instance();
        var run = Run(instance, [Requested(Package("Foo"))]);
        _downloader.Cached.Add("Foo");

        await Service().RunAsync(run, 4, CancellationToken.None);

        Assert.Empty(_downloader.Downloaded);
        Assert.True(run.Jobs[0].IsCached);
        Assert.True(Exists("GameData/Foo/Foo.cfg"));
    }

    [Fact]
    public async Task A_finished_download_is_installed_while_others_still_download()
    {
        var instance = Instance();
        var run = Run(instance, [Requested(Package("Fast")), Requested(Package("Slow"))]);
        var gate = new TaskCompletionSource();
        _downloader.Gates["Slow"] = gate;

        var task = Service().RunAsync(run, 4, CancellationToken.None);
        var fast = run.Jobs.Single(j => j.Version.Identifier == "Fast");
        for (var i = 0; i < 200 && fast.State != ModInstallState.Done; i++) await Task.Delay(10);

        Assert.Equal(ModInstallState.Done, fast.State);
        Assert.True(Exists("GameData/Fast/Fast.cfg"));
        Assert.Equal(ModInstallState.Downloading, run.Jobs.Single(j => j.Version.Identifier == "Slow").State);

        gate.SetResult();
        await task;
        Assert.True(Exists("GameData/Slow/Slow.cfg"));
    }

    [Fact]
    public async Task Parallel_downloads_are_limited_by_the_setting()
    {
        var instance = Instance();
        var mods = Enumerable.Range(1, 6).Select(i => Requested(Package($"Mod{i}"))).ToList();

        await Service().RunAsync(Run(instance, mods), 2, CancellationToken.None);

        Assert.Equal(2, _downloader.MaxActive);
        Assert.Equal(6, instance.Mods.Count);
    }

    [Fact]
    public async Task A_failed_download_rolls_back_everything()
    {
        var instance = Instance();
        var run = Run(instance, [Requested(Package("Good")), Requested(Package("Bad"))]);
        _downloader.Failing.Add("Bad");
        var gate = new TaskCompletionSource();
        _downloader.Gates["Bad"] = gate;

        var task = Service().RunAsync(run, 4, CancellationToken.None);
        var good = run.Jobs.Single(j => j.Version.Identifier == "Good");
        for (var i = 0; i < 200 && good.State != ModInstallState.Done; i++) await Task.Delay(10);
        Assert.True(Exists("GameData/Good/Good.cfg"));
        gate.SetResult();

        var error = await Assert.ThrowsAsync<ModInstallException>(() => task);
        Assert.Contains("Bad 1.0", error.Message);
        Assert.False(Exists("GameData/Good/Good.cfg"));
        Assert.False(Directory.Exists(Path.Combine(_root, "GameData", "Good")));
        Assert.True(Directory.Exists(Path.Combine(_root, "GameData")));
        Assert.Empty(instance.Mods);
        Assert.Equal(0, _saves);
        Assert.Equal(ModInstallState.Failed, run.Jobs.Single(j => j.Version.Identifier == "Bad").State);
    }

    [Fact]
    public async Task An_existing_file_no_mod_owns_fails_the_install_and_is_left_alone()
    {
        var instance = Instance();
        Directory.CreateDirectory(Path.Combine(_root, "GameData", "Foo"));
        await File.WriteAllTextAsync(Path.Combine(_root, "GameData", "Foo", "Foo.cfg"), "manual");

        var error = await Assert.ThrowsAsync<ModInstallException>(() =>
            Service().RunAsync(Run(instance, [Requested(Package("Foo"))]), 4, CancellationToken.None));

        Assert.Contains("GameData/Foo/Foo.cfg", error.Message);
        Assert.Contains("no mod installed it", error.Message);
        Assert.Equal("manual", await File.ReadAllTextAsync(Path.Combine(_root, "GameData", "Foo", "Foo.cfg")));
        Assert.Empty(instance.Mods);
    }

    [Fact]
    public async Task A_file_of_another_installed_mod_names_that_mod()
    {
        var instance = Instance();
        Installed(instance, Version("Other", "1.0"), "GameData/Foo/Foo.cfg");

        var error = await Assert.ThrowsAsync<ModInstallException>(() =>
            Service().RunAsync(Run(instance, [Requested(Package("Foo"))]), 4, CancellationToken.None));

        Assert.Contains("installed by Other", error.Message);
    }

    [Fact]
    public async Task A_new_version_replaces_the_old_files()
    {
        var instance = Instance();
        Installed(instance, Version("Foo", "1.0"), "GameData/Foo/Foo.cfg", "GameData/Foo/Old/old.cfg");

        await Service().RunAsync(Run(instance, [Requested(Package("Foo", "2.0"))]), 4, CancellationToken.None);

        Assert.Equal("Foo 2.0", await File.ReadAllTextAsync(Path.Combine(_root, "GameData", "Foo", "Foo.cfg")));
        Assert.False(Directory.Exists(Path.Combine(_root, "GameData", "Foo", "Old")));
        Assert.Equal("2.0", instance.Mods.Single().Version);
        Assert.Equal(["GameData/Foo/Foo.cfg"], instance.ModRecords["Foo"].Files);
    }

    [Fact]
    public async Task Removed_mods_are_deleted_on_success_and_restored_on_failure()
    {
        var instance = Instance();
        var old = Version("Old", "1.0");
        Installed(instance, old, "GameData/Old/Old.cfg");
        _downloader.Failing.Add("Bad");

        await Assert.ThrowsAsync<ModInstallException>(() =>
            Service().RunAsync(Run(instance, [Requested(Package("Bad"))], old), 4, CancellationToken.None));
        Assert.True(Exists("GameData/Old/Old.cfg"));
        Assert.Contains(old, instance.Mods);

        await Service().RunAsync(Run(instance, [Requested(Package("New"))], old), 4, CancellationToken.None);
        Assert.False(Exists("GameData/Old/Old.cfg"));
        Assert.False(Directory.Exists(Path.Combine(_root, "GameData", "Old")));
        Assert.Equal(["New"], instance.Mods.Select(m => m.Identifier));
        Assert.False(instance.ModRecords.ContainsKey("Old"));
    }

    [Fact]
    public async Task Cancelling_rolls_back()
    {
        var instance = Instance();
        var run = Run(instance, [Requested(Package("Fast")), Requested(Package("Slow"))]);
        _downloader.Gates["Slow"] = new TaskCompletionSource();
        using var cancel = new CancellationTokenSource();

        var task = Service().RunAsync(run, 4, cancel.Token);
        for (var i = 0; i < 200 && run.Jobs[0].State != ModInstallState.Done; i++) await Task.Delay(10);
        await cancel.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        Assert.False(Exists("GameData/Fast/Fast.cfg"));
        Assert.Empty(instance.Mods);
        Assert.Equal(ModInstallState.Cancelled, run.Jobs[1].State);
    }

    [Fact]
    public async Task Folders_outside_GameData_must_exist()
    {
        var instance = Instance();
        var craft = Package("Craft", "1.0", "Craft/My.craft");
        craft.Install = [new InstallDirective { File = "Craft/My.craft", InstallTo = "Ships/VAB" }];
        var missions = Package("Spaceplane", "1.0", "M/x.craft");
        missions.Install = [new InstallDirective { File = "M", InstallTo = "Ships/SPH" }];

        await Service().RunAsync(Run(instance, [Requested(craft)]), 4, CancellationToken.None);
        Assert.True(Exists("Ships/VAB/My.craft"));

        var error = await Assert.ThrowsAsync<ModInstallException>(() =>
            Service().RunAsync(Run(instance, [Requested(missions)]), 4, CancellationToken.None));
        Assert.Contains("doesn't exist", error.Message);
    }

    [Fact]
    public void Check_before_install_lists_every_problem()
    {
        var noUrl = Version("NoUrl", "1.0");
        var meta = Version("Meta", "1.0");
        meta.Kind = "metapackage";
        var dlc = Version("MakingHistory-DLC", "1.0");
        dlc.Kind = "dlc";
        var instance = Instance();

        var errors = InstallService.CheckBeforeInstall(
            Run(instance, [Requested(noUrl), Requested(meta), Requested(dlc)]));

        Assert.Equal(2, errors.Count);
        Assert.Contains(errors, e => e.Contains("NoUrl 1.0 has no download URL"));
        Assert.Contains(errors, e => e.Contains("MakingHistory-DLC is a paid DLC"));

        Directory.CreateDirectory(Path.Combine(_root, "GameData", "SquadExpansion", "MakingHistory"));
        Assert.Single(InstallService.CheckBeforeInstall(Run(instance, [Requested(noUrl), Requested(dlc)])));
    }

    [Fact]
    public async Task Metapackages_and_detected_dlc_are_recorded_without_files()
    {
        var meta = Version("Meta", "1.0");
        meta.Kind = "metapackage";
        var instance = Instance();

        await Service().RunAsync(Run(instance, [Requested(meta)]), 4, CancellationToken.None);

        Assert.Equal(["Meta"], instance.Mods.Select(m => m.Identifier));
        Assert.Empty(instance.ModRecords["Meta"].Files);
    }
    
    [Fact]
    public async Task Files_are_readable_whatever_permissions_the_zip_stored()
    {
        if (OperatingSystem.IsWindows()) return;
        var instance = Instance();
        _downloader.StoredModes["Odd"] = UnixFileMode.UserWrite;
        _downloader.StoredModes["Tool"] = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;

        await Service().RunAsync(Run(instance, [Requested(Package("Odd")), Requested(Package("Tool"))]), 4,
            CancellationToken.None);

        var odd = File.GetUnixFileMode(Path.Combine(_root, "GameData", "Odd", "Odd.cfg"));
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead,
            odd);
        Assert.True(File.GetUnixFileMode(Path.Combine(_root, "GameData", "Tool", "Tool.cfg"))
            .HasFlag(UnixFileMode.UserExecute | UnixFileMode.OtherRead));
        Assert.Equal("Odd 1.0", await File.ReadAllTextAsync(Path.Combine(_root, "GameData", "Odd", "Odd.cfg")));
    }
}