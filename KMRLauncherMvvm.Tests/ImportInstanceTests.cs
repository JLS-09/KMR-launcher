using System.Text.Json;
using KMRLauncherMvvm.Models;
using KMRLauncherMvvm.Services;
using KMRLauncherMvvm.ViewModels.Discover;
using KMRLauncherMvvm.ViewModels.NewInstance;

namespace KMRLauncherMvvm.Tests;

public class ImportInstanceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "kmr-import-" + Guid.NewGuid().ToString("N"));
    private readonly List<Instance> _saved = [];

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    private string Write(string relative, string text = "x")
    {
        var path = Path.Combine(_root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
        return path;
    }

    private static object Module(string identifier, string version, string? kind = null) => new Dictionary<string, object?>
    {
        ["spec_version"] = "v1.34",
        ["identifier"] = identifier,
        ["version"] = version,
        ["license"] = "MIT",
        ["kind"] = kind
    };

    private string KspFolder(string name = "Kerbal Space Program", string buildId = "03190", bool registry = true,
        string? compatible = """{"GameVersionWhenWritten":"1.12.5","Versions":["1.12.5","1.12","1.11.2.3047"]}""")
    {
        var folder = Path.Combine(_root, name);
        Write($"{name}/buildID64.txt", $"build id = {buildId}\n");
        Write($"{name}/GameData/Squad/parts.cfg");
        Write($"{name}/GameData/Kopernicus/Plugins/Kopernicus.dll");
        Write($"{name}/GameData/ModuleManager.4.2.3.dll");
        Write($"{name}/GameData/Extra/thing.cfg");
        if (registry)
        {
            var modules = new Dictionary<string, object>
            {
                ["Kopernicus"] = new
                {
                    source_module = Module("Kopernicus", "release-1.12.1-200"),
                    auto_installed = false,
                    installed_files = new Dictionary<string, object>
                    {
                        ["GameData/Kopernicus/Plugins/Kopernicus.dll"] = new { }, ["GameData/Kopernicus/"] = new { }
                    }
                },
                ["ModuleManager"] = new
                {
                    source_module = Module("ModuleManager", "4.2.3"),
                    auto_installed = true,
                    installed_files = new Dictionary<string, object> { ["GameData/ModuleManager.4.2.3.dll"] = new { } }
                },
                ["MakingHistory-DLC"] = new { source_module = Module("MakingHistory-DLC", "1.13.0", "dlc") },
                ["Broken"] = new { auto_installed = false }
            };
            Write($"{name}/CKAN/registry.json", JsonSerializer.Serialize(new { registry_version = 3, installed_modules = modules }));
        }

        if (compatible is not null) Write($"{name}/CKAN/compatible_ksp_versions.json", compatible);
        return folder;
    }

    private InstanceCreator Creator() => new(new ZipService(), (_, instance) => _saved.Add(instance));

    [Fact]
    public void ScanReadsVersionRegistryCompatibleAndUnownedFolders()
    {
        var scan = KspFolderScanner.Scan(KspFolder(), []);

        Assert.True(scan.CanAdd);
        Assert.Equal("1.12.5", scan.Version);
        Assert.True(scan.HasRegistry);
        Assert.Equal(["Kopernicus", "ModuleManager"], scan.Mods.Select(m => m.Version.Identifier));
        Assert.Equal((1, 1, 1), (scan.Picked, scan.Dependencies, scan.UnreadableMods));
        Assert.Equal(["GameData/Kopernicus/Plugins/Kopernicus.dll"], scan.Mods[0].Record.Files);
        Assert.False(scan.Mods[1].Record.IsUserRequested);
        Assert.Equal(["1.12", "1.11.2"], scan.CkanCompatible);
        Assert.Equal(["Squad", "Extra"], scan.UnownedFolders);
    }

    [Fact]
    public void RegistryModulesWrittenByCkanAreRead()
    {
        const string registry = """
        {
          "registry_version": 3,
          "installed_modules": {
            "Kopernicus": {
              "install_time": "2026-11-20T21:04:11.1234567+01:00",
              "source_module": {
                "spec_version": "v1.16",
                "identifier": "Kopernicus",
                "name": "Kopernicus Planetary System Modifier",
                "abstract": "Replaces and modifies planetary systems",
                "author": "R-T-B",
                "version": "2:release-1.12.1-200",
                "ksp_version_min": "1.8.0",
                "ksp_version_max": "1.12.5",
                "license": "LGPL-3.0",
                "release_status": "stable",
                "resources": { "homepage": "https://forum.kerbalspaceprogram.com/", "repository": "https://github.com/Kopernicus/Kopernicus" },
                "tags": [ "plugin", "library" ],
                "depends": [
                  { "name": "ModuleManager", "min_version": "4.1.0" },
                  { "any_of": [ { "name": "Parallax" }, { "name": "Scatterer" } ], "choice_help_text": "Pick one" }
                ],
                "install": [
                  { "find": "Kopernicus", "install_to": "GameData", "filter": ".gitkeep" },
                  { "find": "ModularFlightIntegrator" },
                  { "file": "README.md", "install_to": "GameRoot" }
                ],
                "download": [ "https://github.com/Kopernicus/Kopernicus/releases/download/x.zip", "https://archive.org/x.zip" ],
                "download_size": 4402010,
                "download_hash": { "sha1": "ABC", "sha256": "DEF" },
                "install_size": 9876543,
                "release_date": "2023-04-12T19:52:47Z"
              },
              "installed_files": { "GameData/Kopernicus/Plugins/Kopernicus.dll": {} },
              "auto_installed": false
            }
          }
        }
        """;
        var path = Write("ckan-format/registry.json", registry);

        var (mods, unreadable, found) = KspFolderScanner.ReadRegistry(path, null);

        Assert.True(found);
        Assert.Empty(unreadable);
        var version = Assert.Single(mods).Version;
        Assert.Equal("2:release-1.12.1-200", version.Version);
        Assert.Equal(["GameData", "GameData", "GameRoot"], version.Install!.Select(d => d.InstallTo));
        Assert.Equal(2, version.Depends!.Count);
    }

    [Fact]
    public void ModulesThatCantBeReadAreNamed()
    {
        var scan = KspFolderScanner.Scan(KspFolder(), []);

        Assert.Equal(["Broken"], scan.Unreadable);
        Assert.Contains("couldn't be read and stay unmanaged: Broken.",
            NewInstanceDialogViewModel.DescribeScan(scan).Single(r => r.Title.StartsWith("CKAN registry")).Hint);
    }

    private string RegistryWith(params (string Id, string Version, bool Auto, string[] Depends)[] mods)
    {
        var modules = mods.ToDictionary(m => m.Id, m => (object)new
        {
            source_module = new Dictionary<string, object?>
            {
                ["spec_version"] = "v1.4",
                ["identifier"] = m.Id,
                ["version"] = m.Version,
                ["license"] = "MIT",
                ["ksp_version"] = "1.12",
                ["depends"] = m.Depends.Select(d => new { name = d }).ToArray()
            },
            auto_installed = m.Auto,
            installed_files = new Dictionary<string, object> { [$"GameData/{m.Id}/{m.Id}.dll"] = new { } }
        });
        return Write("registry/registry.json", JsonSerializer.Serialize(new { installed_modules = modules }));
    }

    private static Instance ImportedInstance(List<ImportedMod> mods) => new("Imported", "/ksp", "1.12.5")
    {
        CompatibleKspVersions = ["1.12"],
        Mods = [.. mods.Select(m => m.Version)],
        ModRecords = mods.ToDictionary(m => m.Version.Identifier, m => m.Record)
    };

    [Fact]
    public void FallbackIdsFollowCkanMetaFileNames()
    {
        var (mods, _, _) = KspFolderScanner.ReadRegistry(RegistryWith(("Kopernicus", "2:release-1.12.1-200", false, [])), null);

        Assert.Equal("Kopernicus-2-release-1.12.1-200", Assert.Single(mods).Version.Id);
    }

    [Fact]
    public void ImportedModsGetUpdatesFromCkanMeta()
    {
        var (mods, _, _) = KspFolderScanner.ReadRegistry(RegistryWith(("Parallax", "2.0.7", false, [])), null);
        var instance = ImportedInstance(mods);
        var listed = TestData.Mod(TestData.Version("Parallax", "2.0.8"), TestData.Version("Parallax", "2.0.7"));

        var fit = ModFit.For(listed, instance, GameVersionCriteria.FromInstance(instance));

        Assert.Equal(KMRLauncherMvvm.Controls.ModStatusKind.Update, fit.Kind);
        Assert.Equal("2.0.8", fit.NewestFit!.Version);
    }

    [Fact]
    public void RemovingAnImportedModAlsoRemovesItsAutoInstalledDependency()
    {
        var (mods, _, _) = KspFolderScanner.ReadRegistry(RegistryWith(
            ("Parallax", "2.0.8", false, ["Kopernicus"]),
            ("Kopernicus", "release-1.12.1-200", true, []),
            ("Waterfall", "0.10.4", false, [])), null);
        var instance = ImportedInstance(mods);

        var plan = InstalledMods.PlanRemoval(instance, ["Parallax"]);

        Assert.Equal(["Parallax", "Kopernicus"], plan.All.Select(m => m.Identifier));
    }

    [Fact]
    public void RequestingTheInstalledVersionOfAnImportedModChangesNothing()
    {
        var (mods, _, _) = KspFolderScanner.ReadRegistry(RegistryWith(("Kopernicus", "2:release-1.12.1-200", false, [])), null);
        var instance = ImportedInstance(mods);
        var listed = TestData.Version("Kopernicus", "2:release-1.12.1-200");
        listed.Id = "Kopernicus-2-release-1.12.1-200-from-ckan-meta";
        var services = new TestData.TestServices(TestData.Mod(listed));
        var data = new InstallModsData { SelectedInstance = instance, RequestedModVersions = [TestData.Requested(listed)] };

        services.Dependency.ResolveDependencies(data);

        Assert.Empty(data.Problems);
        Assert.DoesNotContain(data.FinalModList, m => m.Version.Identifier == "Kopernicus");
    }

    [Fact]
    public void OlderCompatibleFileFormatIsRead()
    {
        var folder = KspFolder(compatible: """{"VersionOfKspWhenWritten":"1.12.5","CompatibleKspVersions":["1.11","1.10"]}""");

        Assert.Equal(["1.11", "1.10"], KspFolderScanner.Scan(folder, []).CkanCompatible);
    }

    [Fact]
    public void KnownCkanMetaVersionsAreUsedInsteadOfTheRegistryCopy()
    {
        var known = TestData.Version("Kopernicus", "release-1.12.1-200");

        var scan = KspFolderScanner.Scan(KspFolder(), [], [TestData.Mod(known)]);

        Assert.Same(known, scan.Mods.Single(m => m.Version.Identifier == "Kopernicus").Version);
        Assert.Equal("ModuleManager-4.2.3", scan.Mods.Single(m => m.Version.Identifier == "ModuleManager").Version.Id);
    }

    [Fact]
    public void FolderWithoutCkanHasNoModsOrCompatibleVersions()
    {
        var scan = KspFolderScanner.Scan(KspFolder(registry: false, compatible: null), []);

        Assert.True(scan.CanAdd);
        Assert.False(scan.HasRegistry);
        Assert.Empty(scan.Mods);
        Assert.Null(scan.CkanCompatible);
        Assert.Equal(["Squad", "Extra", "Kopernicus"], scan.UnownedFolders);
    }

    [Fact]
    public void FoldersThatAreNotKspOrAlreadyListedAreRefused()
    {
        Directory.CreateDirectory(Path.Combine(_root, "empty"));
        var ksp = KspFolder();

        Assert.False(KspFolderScanner.Scan(Path.Combine(_root, "empty"), []).CanAdd);
        Assert.False(KspFolderScanner.Scan(Path.Combine(_root, "nowhere"), []).CanAdd);
        Assert.False(KspFolderScanner.Scan(KspFolder("odd", buildId: "99999"), []).CanAdd);
        Assert.Contains("already",
            KspFolderScanner.Scan(ksp, [new Instance("Old", ksp, "1.12.5")]).Error);
    }

    [Fact]
    public async Task ImportInPlaceUsesTheFolderAndTheCkanData()
    {
        var folder = KspFolder();
        var settings = new AppSettings();
        var scan = KspFolderScanner.Scan(folder, []);

        var instance = await Creator().ImportAsync(settings,
            new ImportRequest("Steam 1.12.5", scan, ImportMode.InPlace, "", IsPrimary: true));

        Assert.Equal(folder, instance.RootPath);
        Assert.Equal(["Kopernicus", "ModuleManager"], instance.Mods.Select(m => m.Identifier));
        Assert.True(instance.ModRecords["Kopernicus"].IsUserRequested);
        Assert.False(instance.ModRecords["ModuleManager"].IsUserRequested);
        Assert.Equal(["1.12", "1.11.2"], instance.CompatibleKspVersions);
        Assert.True(instance.IsPrimary);
        Assert.Same(instance, Assert.Single(_saved));
    }

    [Fact]
    public async Task ImportByCopyLeavesTheOriginalAlone()
    {
        var folder = KspFolder();
        Write("Kerbal Space Program/.kmr/staging/old/leftover.txt");
        var target = Path.Combine(_root, "instances", "steam-1.12.5");
        var scan = KspFolderScanner.Scan(folder, []);

        var instance = await Creator().ImportAsync(new AppSettings(),
            new ImportRequest("Steam 1.12.5", scan, ImportMode.Copy, target, IsPrimary: false));

        Assert.Equal(target, instance.RootPath);
        Assert.True(File.Exists(Path.Combine(target, "GameData", "Kopernicus", "Plugins", "Kopernicus.dll")));
        Assert.True(File.Exists(Path.Combine(target, "CKAN", "registry.json")));
        Assert.False(Directory.Exists(Path.Combine(target, ".kmr", "staging")));
        Assert.True(File.Exists(Path.Combine(folder, "GameData", "Squad", "parts.cfg")));
        Assert.Equal(2, instance.Mods.Count);
    }

    [Fact]
    public async Task CancelledCopyIsDeleted()
    {
        var folder = KspFolder();
        for (var i = 0; i < 100; i++) Write($"Kerbal Space Program/GameData/Squad/f{i}.cfg");
        var target = Path.Combine(_root, "instances", "copy");
        using var cancel = new CancellationTokenSource();
        var progress = new SyncProgress<CreateProgress>(p =>
        {
            if (p.Unpack is { FilesDone: >= 5 }) cancel.Cancel();
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Creator().ImportAsync(new AppSettings(),
            new ImportRequest("Copy", KspFolderScanner.Scan(folder, []), ImportMode.Copy, target, false), progress,
            cancel.Token));

        Assert.False(Directory.Exists(target));
        Assert.True(File.Exists(Path.Combine(folder, "buildID64.txt")));
        Assert.Empty(_saved);
    }

    [Fact]
    public async Task CopyIntoTheFolderItselfIsRefused()
    {
        var folder = KspFolder();
        var scan = KspFolderScanner.Scan(folder, []);

        await Assert.ThrowsAsync<IOException>(() => Creator().ImportAsync(new AppSettings(),
            new ImportRequest("Inside", scan, ImportMode.Copy, Path.Combine(folder, "copy"), false)));
    }

    [Fact]
    public async Task FolderWithKmrDataIsLoadedAndACopyGetsItsOwnPrefix()
    {
        var folder = KspFolder();
        var old = new Instance("Old name", folder, "1.12.5")
        {
            PrefixPath = "/prefixes/old",
            PlayTime = 42,
            Mods = [TestData.Version("Parallax", "2.0.8")],
            ModRecords = { ["Parallax"] = new InstalledModRecord { Files = ["GameData/Parallax/p.dll"], IsUserRequested = true } }
        };
        InstanceStore.Save(old);
        var scan = KspFolderScanner.Scan(folder, []);
        Assert.NotNull(scan.KmrData);

        var copy = await Creator().ImportAsync(new AppSettings(),
            new ImportRequest("New name", scan, ImportMode.Copy, Path.Combine(_root, "copy"), false));

        Assert.Equal("New name", copy.Name);
        Assert.Equal(42, copy.PlayTime);
        Assert.Equal(["Parallax"], copy.Mods.Select(m => m.Identifier));
        Assert.Equal("", copy.PrefixPath);
    }

    [Fact]
    public async Task ForgottenInstanceComesBackInPlaceWithItsData()
    {
        var folder = KspFolder();
        var forgotten = new Instance("Career", folder, "1.12.5")
        {
            PrefixPath = "/prefixes/career",
            PlayTime = 3600,
            CompatibleKspVersions = ["1.11"]
        };
        InstanceStore.Save(forgotten);

        var back = await Creator().ImportAsync(new AppSettings(),
            new ImportRequest("Career", KspFolderScanner.Scan(folder, []), ImportMode.InPlace, "", false));

        Assert.Equal(folder, back.RootPath);
        Assert.Equal("/prefixes/career", back.PrefixPath);
        Assert.Equal(3600, back.PlayTime);
        Assert.Equal(["1.11"], back.CompatibleKspVersions);
    }

    [Fact]
    public async Task DialogScansAndAddsInPlace()
    {
        var folder = KspFolder();
        var settings = new AppSettings { InstanceParentFolder = Path.Combine(_root, "instances") };
        Instance? finished = null;
        var vm = new NewInstanceDialogViewModel(settings, new ZipService(), Creator(), i => finished = i)
        {
            IsFolderTab = true
        };

        await vm.ScanAsync(folder);

        Assert.Equal("Kerbal Space Program 1.12.5", vm.ImportName);
        Assert.Equal(["KSP 1.12.5", "CKAN registry with 2 mods", "CKAN compatible versions",
            "2 folders in GameData no mod owns"], vm.FoundRows.Select(r => r.Title));
        Assert.Contains(new SummaryRow("Mods", "2 from CKAN"), vm.Summary);
        Assert.Contains(new SummaryRow("Compatible", "1.12, 1.11.2"), vm.Summary);
        Assert.Equal("Add instance", vm.CreateText);

        vm.IsCopy = false;
        await vm.CreateCommand.ExecuteAsync(null);

        Assert.NotNull(finished);
        Assert.Equal(folder, finished.RootPath);
    }

    [Fact]
    public async Task DialogBlocksAddingAFolderThatIsNoKspInstall()
    {
        Directory.CreateDirectory(Path.Combine(_root, "empty"));
        var vm = new NewInstanceDialogViewModel(new AppSettings(), new ZipService(), Creator(), _ => { })
        {
            IsFolderTab = true
        };

        await vm.ScanAsync(Path.Combine(_root, "empty"));

        Assert.Equal(FoundKind.Error, Assert.Single(vm.FoundRows).Kind);
        Assert.False(vm.CreateCommand.CanExecute(null));
    }

    private sealed class SyncProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}
