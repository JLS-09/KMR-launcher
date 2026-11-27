using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using KMRLauncherMvvm.Models;
using LibGit2Sharp;

namespace KMRLauncherMvvm.Services.Api;

public class ModGitService(GitHelper gitHelper, ModListService modList, SyncStatusService syncStatus) : IModApiService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private static string ModsCacheFile => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "kmrLauncher/mods.json");

    private readonly SemaphoreSlim _lock = new(1, 1);

    public bool HasCache => Repository.IsValid(GitHelper.DefaultCkanMetaFolder) && File.Exists(ModsCacheFile);

    public async Task GetAllModsAsync(IProgress<ModFetchProgress>? progress, bool isRefresh, bool cacheOnly = false,
        CancellationToken cancel = default)
    {
        await _lock.WaitAsync();
        try
        {
            var gitCacheFolder = GitHelper.DefaultCkanMetaFolder;
            if (cacheOnly && Repository.IsValid(gitCacheFolder) && await TryLoadCache(progress)) return;

            var cancelled = false;
            syncStatus.SetFetching(true);
            try
            {
                await FetchAndLoad(gitCacheFolder, progress, isRefresh, cancel);
            }
            catch (Exception) when (cancel.IsCancellationRequested && Repository.IsValid(gitCacheFolder))
            {
                cancelled = true;
            }
            catch (Exception e)
            {
                syncStatus.RecordFailure(e.Message);
                throw;
            }
            finally
            {
                syncStatus.SetFetching(false);
            }

            if (cancelled && !await TryLoadCache(progress)) await gitHelper.PopulateMods(gitCacheFolder, progress);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task FetchAndLoad(string gitCacheFolder, IProgress<ModFetchProgress>? progress, bool isRefresh,
        CancellationToken cancel)
    {
        var latestCommitHash = Repository.ListRemoteReferences(GitHelper.CkanMetaUrl)
            .First(r => r.CanonicalName == $"refs/heads/{GitHelper.CkanMetaBranch}").TargetIdentifier;
        cancel.ThrowIfCancellationRequested();

        var upToDate = false;
        if (!Directory.Exists(gitCacheFolder))
        {
            GitHelper.CloneCkanMetaRepo(gitCacheFolder, progress, cancel);
        }
        else
        {
            upToDate = CurrentCommit(gitCacheFolder) == latestCommitHash;
            if (!upToDate || isRefresh) GitHelper.PullCkanMetaRepo(gitCacheFolder, progress, cancel);
        }

        syncStatus.RecordSuccess(CurrentCommit(gitCacheFolder));

        if (upToDate && !isRefresh && await TryLoadCache(progress)) return;

        await gitHelper.PopulateMods(gitCacheFolder, progress);
        var cache = new ModsCache { Mods = [..modList.Mods!], CurrentCommitHash = latestCommitHash };
        await using (var stream = File.Create(ModsCacheFile))
            await JsonSerializer.SerializeAsync(stream, cache, Options);
    }

    private async Task<bool> TryLoadCache(IProgress<ModFetchProgress>? progress)
    {
        if (!File.Exists(ModsCacheFile)) return false;

        progress?.Report(new ModFetchProgress
        {
            TotalMods = 1,
            ModsReceived = 1,
            CurrentModName = "Loading cache",
            IsCache = true
        });

        ModsCache? modsCache;
        await using (var stream = File.OpenRead(ModsCacheFile))
            modsCache = await JsonSerializer.DeserializeAsync<ModsCache>(stream, Options);
        if (modsCache is null) return false;

        modList.Mods = new ObservableCollection<Mod>(modsCache.Mods);
        return true;
    }

    private static string CurrentCommit(string gitCacheFolder)
    {
        using var repo = new Repository(gitCacheFolder);
        return repo.Refs.First(r => r.CanonicalName == $"refs/heads/{GitHelper.CkanMetaBranch}").TargetIdentifier;
    }
}
