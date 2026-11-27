using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Avalonia.Threading;
using KMRLauncherMvvm.Models;

namespace KMRLauncherMvvm.Services;

public class HomeDataService
{
    public static string DefaultFile => Path.Combine(AppInfo.DataFolder, "home.json");

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    private readonly string _file;
    private readonly InstanceService _instances;
    private readonly ModListService _modList;
    private readonly SyncStatusService _syncStatus;
    private readonly DownloadCache _cache;
    private readonly object _lock = new();
    private readonly Func<IReadOnlyDictionary<string, long>> _readDownloadCounts;
    private IReadOnlyDictionary<string, long> _downloadCounts = new Dictionary<string, long>();
    private string? _downloadCountsCommit;

    public const int PopularCount = 6;

    public HomeData Data { get; private set; }

    public event Action? Changed;

    public HomeDataService(InstanceService instances, ModListService modList, SyncStatusService syncStatus,
        DownloadCache cache) : this(DefaultFile, instances, modList, syncStatus, cache,
        () => GitHelper.ReadDownloadCounts(GitHelper.DefaultCkanMetaFolder))
    {
        instances.Changed += () => Recheck();
        modList.ModsChanged += () => Dispatcher.UIThread.Post(() => Recheck());
        cache.Changed += () => Dispatcher.UIThread.Post(() => Recheck());
    }

    public HomeDataService(string file, InstanceService instances, ModListService modList,
        SyncStatusService syncStatus, DownloadCache cache, Func<IReadOnlyDictionary<string, long>> readDownloadCounts)
    {
        _readDownloadCounts = readDownloadCounts;
        _file = file;
        _instances = instances;
        _modList = modList;
        _syncStatus = syncStatus;
        _cache = cache;
        Data = Load(file);
    }

    public InstanceCheckResult? For(Instance instance) =>
        Data.Instances.FirstOrDefault(i => i.Folder == instance.RootPath);

    public HomeData Recheck()
    {
        lock (_lock)
        {
            var mods = _modList.Mods?.ToDictionary(m => m.Id);
            var data = mods is null
                ? new HomeData { Commit = Data.Commit, CheckedAt = Data.CheckedAt, Instances = Data.Instances }
                : new HomeData
                {
                    Commit = _syncStatus.Status.LastCommit,
                    CheckedAt = DateTimeOffset.Now,
                    Instances = [.. _instances.Usable.Select(i => Check(i, mods))]
                };

            data.CkanChanges = Data.CkanChanges;
            (data.PopularFor, data.Popular) = mods is null
                ? (Data.PopularFor, Data.Popular)
                : (_instances.Primary?.RootPath, FindPopular(_instances.Primary, mods));
            var usage = MeasureCache();
            data.UnusedDownloads = usage.UnusedCount;
            data.UnusedDownloadBytes = usage.UnusedBytes;
            Data = data;
            Save();
        }

        Changed?.Invoke();
        return Data;
    }

    public void SaveCkanChanges(CkanChanges changes)
    {
        lock (_lock)
        {
            Data.CkanChanges = changes;
            Save();
        }
    }

    private List<PopularMod> FindPopular(Instance? primary, Dictionary<string, Mod> mods)
    {
        if (primary is null) return [];

        if (_downloadCountsCommit != _syncStatus.Status.LastCommit || _downloadCounts.Count == 0)
        {
            _downloadCounts = _readDownloadCounts();
            _downloadCountsCommit = _syncStatus.Status.LastCommit;
        }

        var criteria = GameVersionCriteria.FromInstance(primary);
        HashSet<string> installed = [.. primary.Mods.Select(m => m.Identifier)];
        return
        [
            .. _downloadCounts
                .OrderByDescending(c => c.Value)
                .Where(c => !installed.Contains(c.Key) && mods.TryGetValue(c.Key, out var mod) &&
                            mod.Versions.Count > 0 && mod.Versions[0].Kind is null or "package" &&
                            InstanceChecks.NewestFit(mod, criteria) is not null)
                .Take(PopularCount)
                .Select(c => new PopularMod(c.Key, c.Value))
        ];
    }

    private CacheUsage MeasureCache()
    {
        try
        {
            return _cache.Measure(_instances.Instances.ToList());
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return new CacheUsage(0, 0, 0, 0);
        }
    }

    private static InstanceCheckResult Check(Instance instance, Dictionary<string, Mod> mods)
    {
        var checks = InstanceChecks.CheckAll(instance, mods);
        return new InstanceCheckResult
        {
            Folder = instance.RootPath,
            Updates =
            [
                .. checks.Where(c => c.Update is not null)
                    .Select(c => new ModUpdateResult(c.Installed.Identifier, c.Installed.Version, c.Update!.Version))
            ],
            Replacements =
            [
                .. checks.Where(c => c.Replacement is not null)
                    .Select(c => new ModReplacementResult(c.Installed.Identifier, c.Installed.Version,
                        c.Replacement!.Identifier, c.Replacement.Version))
            ],
            NotListed = [.. checks.Where(c => c.IsNotListed).Select(c => c.Installed.Identifier)]
        };
    }

    private static HomeData Load(string file)
    {
        try
        {
            return File.Exists(file)
                ? JsonSerializer.Deserialize<HomeData>(File.ReadAllText(file), Options) ?? new HomeData()
                : new HomeData();
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException)
        {
            return new HomeData();
        }
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
            AtomicFile.WriteAllText(_file, JsonSerializer.Serialize(Data, Options));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }
}
