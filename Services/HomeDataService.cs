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
    private readonly object _lock = new();

    public HomeData Data { get; private set; }

    public event Action? Changed;

    public HomeDataService(InstanceService instances, ModListService modList, SyncStatusService syncStatus)
        : this(DefaultFile, instances, modList, syncStatus)
    {
        instances.Changed += () => Recheck();
        modList.ModsChanged += () => Dispatcher.UIThread.Post(() => Recheck());
    }

    public HomeDataService(string file, InstanceService instances, ModListService modList,
        SyncStatusService syncStatus)
    {
        _file = file;
        _instances = instances;
        _modList = modList;
        _syncStatus = syncStatus;
        Data = Load(file);
    }

    public InstanceCheckResult? For(Instance instance) =>
        Data.Instances.FirstOrDefault(i => i.Folder == instance.RootPath);

    public HomeData Recheck()
    {
        lock (_lock)
        {
            if (_modList.Mods is not { } modList) return Data;

            var mods = modList.ToDictionary(m => m.Id);
            Data = new HomeData
            {
                Commit = _syncStatus.Status.LastCommit,
                CheckedAt = DateTimeOffset.Now,
                Instances = [.. _instances.Usable.Select(i => Check(i, mods))]
            };
            Save();
        }

        Changed?.Invoke();
        return Data;
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
