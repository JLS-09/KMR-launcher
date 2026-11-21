using System.Text.Json;
using KMRLauncherMvvm.Models;
using KMRLauncherMvvm.Services;
using static KMRLauncherMvvm.Tests.TestData;

namespace KMRLauncherMvvm.Tests;

public class InstanceDataTests : IDisposable
{
    private readonly string _temp = Path.Combine(Path.GetTempPath(), "kmr-data-" + Guid.NewGuid());
    private readonly string _settingsFile;

    public InstanceDataTests()
    {
        Directory.CreateDirectory(_temp);
        _settingsFile = Path.Combine(_temp, "settings.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_temp)) Directory.Delete(_temp, true);
    }

    private string Folder(string name, bool create = true)
    {
        var path = Path.Combine(_temp, name);
        if (create) Directory.CreateDirectory(path);
        return path;
    }

    private static Instance Make(string name, string root, bool primary = false)
    {
        var lib = Version("Lib", "1.2");
        var foo = Version("Foo", "2.0", depends: [Rel("Lib", min: "1.0")]);
        return new Instance(name, root, "1.12.5")
        {
            CompatibleKspVersions = ["1.11", "1.10.1"],
            LaunchArguments = "-force-glcore",
            PrefixPath = root + "-prefix",
            PlayTime = 3600,
            LastPlayed = new DateTime(2026, 11, 20, 18, 30, 0),
            IsPrimary = primary,
            Mods = [foo, lib],
            ModRecords = new Dictionary<string, InstalledModRecord>
            {
                ["Foo"] = new() { Files = ["GameData/Foo/Foo.dll"], IsUserRequested = true },
                ["Lib"] = new() { Files = ["GameData/Lib/Lib.cfg"], Reason = "Recommended by Foo" }
            }
        };
    }

    private void WriteOldSettings(params Instance[] instances)
    {
        var old = new LegacySettingsFile
        {
            KspZips = [new KspZip("/zips/ksp.zip", "KSP_linux", "1.12.5")],
            Instances = [.. instances],
            MaxParallelDownloads = 6
        };
        File.WriteAllText(_settingsFile, JsonSerializer.Serialize(old, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static string DataFile(string root, string name) => Path.Combine(root, ".kmr", name);

    [Fact]
    public void Migration_writes_each_instance_into_its_folder_and_slims_settings()
    {
        var rp1 = Make("RP-1", Folder("rp1"));
        var stock = Make("Stock", Folder("stock"), primary: true);
        WriteOldSettings(rp1, stock);
        var oldText = File.ReadAllText(_settingsFile);

        var settings = SettingsService.Load(_settingsFile);

        Assert.False(settings.UsesLegacyFile);
        Assert.Null(settings.LoadProblem);
        Assert.True(File.Exists(DataFile(rp1.RootPath, "instance.json")));
        Assert.True(File.Exists(DataFile(rp1.RootPath, "mods.json")));
        Assert.Equal(oldText, File.ReadAllText(_settingsFile + ".bak"));

        using var slim = JsonDocument.Parse(File.ReadAllText(_settingsFile));
        Assert.False(slim.RootElement.TryGetProperty("Instances", out _));
        Assert.Equal([rp1.RootPath, stock.RootPath],
            slim.RootElement.GetProperty("InstanceFolders").EnumerateArray().Select(e => e.GetString()));
        Assert.Equal(stock.RootPath, slim.RootElement.GetProperty("PrimaryInstance").GetString());
        Assert.Equal(6, slim.RootElement.GetProperty("MaxParallelDownloads").GetInt32());

        var reloaded = SettingsService.Load(_settingsFile);
        Assert.Equal(["RP-1", "Stock"], reloaded.Instances.Select(i => i.Name));
        Assert.True(InstanceStore.SameData(rp1, reloaded.Instances[0]));
        Assert.True(reloaded.Instances[1].IsPrimary);
        Assert.False(reloaded.Instances[0].IsPrimary);
        Assert.Single(reloaded.KspZips);
        Assert.Empty(Directory.GetFiles(_temp, "*.tmp", SearchOption.AllDirectories));
    }

    [Fact]
    public void Migration_reads_back_ckan_metadata_unchanged()
    {
        const string ckan = """
            {
              "spec_version": 1, "identifier": "Parallax", "name": "Parallax", "abstract": "Terrain",
              "version": "2:2.0.8", "license": "CC-BY-NC-ND-4.0", "ksp_version_min": "1.12",
              "download": "https://example.com/Parallax.zip",
              "download_hash": { "sha1": "AA", "sha256": "BB" }, "download_size": 1234,
              "install_size": 5678, "release_date": "2024-05-01T10:20:30Z",
              "depends": [ { "any_of": [ { "name": "Kopernicus" }, { "name": "KopernicusBE", "min_version": "1.0" } ] } ],
              "install": [ { "find": "Parallax", "install_to": "GameData", "filter": "Thumbs.db" } ]
            }
            """;
        var instance = Make("Real", Folder("real"));
        instance.Mods = [JsonHelper.ParseVersion("Parallax-2.0.8.ckan", ckan)!];
        WriteOldSettings(instance);

        var settings = SettingsService.Load(_settingsFile);

        Assert.Null(settings.LoadProblem);
        var mod = Assert.Single(settings.Instances[0].Mods);
        Assert.Equal("2:2.0.8", mod.Version);
        Assert.Equal(new DateTime(2024, 5, 1, 10, 20, 30, DateTimeKind.Utc), mod.ReleaseDate);
        Assert.Equal("GameData", mod.Install![0].InstallTo);
    }

    [Fact]
    public void Migration_keeps_an_instance_whose_folder_is_gone_as_missing()
    {
        var gone = Make("Gone", Folder("gone", create: false));
        WriteOldSettings(Make("Here", Folder("here")), gone);

        var settings = SettingsService.Load(_settingsFile);

        Assert.False(settings.UsesLegacyFile);
        var missing = settings.Instances[1];
        Assert.True(missing.IsMissing);
        Assert.Equal("Gone", missing.Name);
        Assert.Equal(gone.RootPath, missing.RootPath);
        Assert.Single(settings.UsableInstances);
        Assert.Contains(gone.RootPath, File.ReadAllText(_settingsFile));
    }

    [Fact]
    public void Failed_migration_keeps_using_the_old_file_and_says_so()
    {
        var root = Folder("blocked");
        File.WriteAllText(Path.Combine(root, ".kmr"), "a file where the folder should go");
        WriteOldSettings(Make("Blocked", root));
        var oldText = File.ReadAllText(_settingsFile);

        var settings = SettingsService.Load(_settingsFile);

        Assert.True(settings.UsesLegacyFile);
        Assert.NotNull(settings.LoadProblem);
        Assert.Equal(oldText, File.ReadAllText(_settingsFile));
        Assert.False(File.Exists(_settingsFile + ".bak"));
        Assert.Equal("Blocked", Assert.Single(settings.Instances).Name);

        settings.Instances[0].PlayTime = 7200;
        SettingsService.Write(settings, _settingsFile, settings.Instances[0]);
        using var written = JsonDocument.Parse(File.ReadAllText(_settingsFile));
        Assert.Equal(7200, written.RootElement.GetProperty("Instances")[0].GetProperty("PlayTime").GetInt32());
    }

    [Fact]
    public void Saving_writes_only_the_changed_instance()
    {
        var a = Make("A", Folder("a"));
        var b = Make("B", Folder("b"));
        var settings = new AppSettings { Instances = [a, b] };
        InstanceStore.Save(a);
        InstanceStore.Save(b);
        File.WriteAllText(DataFile(b.RootPath, "instance.json"), "untouched");
        File.WriteAllText(DataFile(a.RootPath, "mods.json"), "untouched");

        a.PlayTime = 99;
        SettingsService.Write(settings, _settingsFile, a);

        Assert.Equal("untouched", File.ReadAllText(DataFile(b.RootPath, "instance.json")));
        Assert.Equal("untouched", File.ReadAllText(DataFile(a.RootPath, "mods.json")));
        Assert.Contains("\"PlayTime\": 99", File.ReadAllText(DataFile(a.RootPath, "instance.json")));

        SettingsService.Write(settings, _settingsFile, a, modsChanged: true);
        Assert.Contains("GameData/Foo/Foo.dll", File.ReadAllText(DataFile(a.RootPath, "mods.json")));
    }

    [Fact]
    public void Instance_json_has_no_folder_primary_flag_or_computed_values()
    {
        var instance = Make("A", Folder("a"), primary: true);
        InstanceStore.Save(instance);

        var json = File.ReadAllText(DataFile(instance.RootPath, "instance.json"));

        Assert.DoesNotContain("RootPath", json);
        Assert.DoesNotContain("IsPrimary", json);
        Assert.DoesNotContain("Mods", json);
        Assert.DoesNotContain("CompatibleSummary", json);
        Assert.Contains("\"PrefixPath\"", json);
    }

    [Fact]
    public void A_missing_folder_or_data_folder_shows_as_missing()
    {
        var gone = Folder("gone", create: false);
        var empty = Folder("empty");
        File.WriteAllText(_settingsFile, JsonSerializer.Serialize(new AppSettings
        {
            InstanceFolders = [gone, empty],
            PrimaryInstance = gone
        }));

        var settings = SettingsService.Load(_settingsFile);

        Assert.All(settings.Instances, i => Assert.True(i.IsMissing));
        Assert.Equal(["gone", "empty"], settings.Instances.Select(i => i.Name));
        Assert.Contains(".kmr/instance.json", settings.Instances[1].LoadError);
        Assert.True(settings.Instances[0].IsPrimary);
        Assert.Empty(settings.UsableInstances);

        SettingsService.Write(settings, _settingsFile, settings.Instances[1]);
        Assert.False(Directory.Exists(Path.Combine(empty, ".kmr")));
        Assert.Contains(gone, File.ReadAllText(_settingsFile));
    }

    [Fact]
    public void A_broken_instance_json_shows_as_missing_and_is_not_overwritten()
    {
        var root = Folder("broken");
        InstanceStore.Save(Make("Broken", root));
        File.WriteAllText(DataFile(root, "instance.json"), "{ \"Name\": ");

        var instance = InstanceStore.Load(root);

        Assert.True(instance.IsMissing);
        Assert.Contains("can't be read", instance.LoadError);

        SettingsService.Write(new AppSettings { Instances = [instance] }, _settingsFile, instance, modsChanged: true);
        Assert.Equal("{ \"Name\": ", File.ReadAllText(DataFile(root, "instance.json")));
        Assert.Contains("GameData/Foo/Foo.dll", File.ReadAllText(DataFile(root, "mods.json")));
    }

    [Fact]
    public void Data_from_a_newer_launcher_is_not_loaded()
    {
        var root = Folder("future");
        InstanceStore.Save(Make("Future", root));
        var path = DataFile(root, "instance.json");
        File.WriteAllText(path, File.ReadAllText(path).Replace("\"Format\": 1", "\"Format\": 2"));

        Assert.Contains("newer version", InstanceStore.Load(root).LoadError);
    }

    [Fact]
    public void Locating_a_moved_folder_replaces_the_missing_entry()
    {
        var moved = Folder("moved");
        InstanceStore.Save(Make("RP-1", moved));
        var missing = InstanceStore.Load(Folder("old-place", create: false));
        missing.IsPrimary = true;
        var other = Make("Other", Folder("other"));
        InstanceStore.Save(other);
        var settings = new AppSettings { Instances = [missing, other] };

        Assert.Equal("That folder is already in the list.",
            InstanceStore.Relocate(settings, missing, other.RootPath).Error);
        Assert.Contains(".kmr/instance.json", InstanceStore.Relocate(settings, missing, Folder("plain")).Error);

        var (found, error) = InstanceStore.Relocate(settings, missing, moved + "/");

        Assert.Null(error);
        Assert.Same(found, settings.Instances[0]);
        Assert.Equal("RP-1", found!.Name);
        Assert.Equal(moved, found.RootPath);
        Assert.True(found.IsPrimary);
        Assert.Equal(2, found.Mods.Count);
    }
}
