using KMRLauncherMvvm.Models;
using KMRLauncherMvvm.Services;

namespace KMRLauncherMvvm.Tests;

public class DownloadCacheTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "kmr-cache-" + Guid.NewGuid().ToString("N"));
    private readonly ModDownloader _downloader;
    private readonly DownloadCache _cache;

    private readonly ModVersion _installed = WithHash(TestData.Version("Kopernicus", "1.0"), "AAAA1111");
    private readonly ModVersion _other = WithHash(TestData.Version("Parallax", "2.0"), "BBBB2222");

    public DownloadCacheTests()
    {
        _downloader = new ModDownloader(new HttpClient(), _folder);
        _cache = new DownloadCache(_folder, _downloader.CachePath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
    }

    private static ModVersion WithHash(ModVersion version, string sha256)
    {
        version.DownloadHash = new DownloadHash { Sha256 = sha256 };
        return version;
    }

    private void Cache(ModVersion mod, int bytes) => WriteFile(_downloader.CachePath(mod), bytes);

    private void WriteFile(string path, int bytes)
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllBytes(path, new byte[bytes]);
    }

    private Instance InstanceWith(params ModVersion[] mods)
    {
        var instance = new Instance("A", "/tmp/a", "1.12.5");
        instance.Mods.AddRange(mods);
        return instance;
    }

    [Fact]
    public void NoFolderMeansEmpty()
    {
        Assert.Equal(new CacheUsage(0, 0, 0, 0), _cache.Measure([InstanceWith(_installed)]));
    }

    [Fact]
    public void ArchivesOfInstalledModsAreInUse()
    {
        Cache(_installed, 300);
        Cache(_other, 100);
        WriteFile(Path.Combine(_folder, "leftover.zip.part"), 50);

        var usage = _cache.Measure([InstanceWith(_installed)]);

        Assert.Equal(300, usage.InUseBytes);
        Assert.Equal(150, usage.UnusedBytes);
        Assert.Equal(3, usage.ArchiveCount);
        Assert.Equal(2, usage.UnusedCount);
    }

    [Fact]
    public void AnArchiveUsedByAnyInstanceCounts()
    {
        Cache(_installed, 300);
        Cache(_other, 100);

        var usage = _cache.Measure([InstanceWith(_installed), InstanceWith(_other)]);

        Assert.Equal(400, usage.InUseBytes);
        Assert.Equal(0, usage.UnusedCount);
    }

    [Fact]
    public void RemoveUnusedKeepsArchivesInUse()
    {
        Cache(_installed, 300);
        Cache(_other, 100);

        var result = _cache.RemoveUnused([InstanceWith(_installed)]);

        Assert.Equal(new CacheCleanup(1, 100, 0), result);
        Assert.True(File.Exists(_downloader.CachePath(_installed)));
        Assert.False(File.Exists(_downloader.CachePath(_other)));
    }

    [Fact]
    public void ClearAllDeletesEverything()
    {
        Cache(_installed, 300);
        Cache(_other, 100);

        var result = _cache.ClearAll();

        Assert.Equal(new CacheCleanup(2, 400, 0), result);
        Assert.Empty(Directory.GetFiles(_folder));
    }
}
