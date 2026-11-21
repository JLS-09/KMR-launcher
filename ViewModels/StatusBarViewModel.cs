using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using KMRLauncherMvvm.Models;
using KMRLauncherMvvm.Services;

namespace KMRLauncherMvvm.ViewModels;

public partial class StatusBarViewModel : ObservableObject
{
    private readonly ModListService? _modListService;
    private readonly SyncStatusService? _syncStatus;

    [ObservableProperty] private string _repoText = "not cloned yet";
    [ObservableProperty] private string _versionsText = "mod list not loaded";
    [ObservableProperty] private string _cacheText = "Cache –";

    [ObservableProperty] private string _syncText = "synced –";
    [ObservableProperty] private string _syncHint = "CKAN-meta hasn't been fetched yet";

    public StatusBarViewModel(ModListService modListService, SyncStatusService syncStatus)
    {
        _modListService = modListService;
        _syncStatus = syncStatus;
        _modListService.ModsChanged += () => Dispatcher.UIThread.Post(() => _ = RefreshAsync());
        _syncStatus.Changed += () => Dispatcher.UIThread.Post(() => _ = RefreshAsync());
        _ = RefreshAsync();
    }

    public StatusBarViewModel()
    {
    }

    public async Task RefreshAsync()
    {
        var (sha, cacheBytes) = await Task.Run(() =>
            (GitHelper.ReadHeadSha(GitHelper.DefaultCkanMetaFolder), ReadCacheSize()));

        RepoText = sha is null ? "not cloned yet" : $"{GitHelper.CkanMetaBranch} @ {sha[..7]}";
        CacheText = cacheBytes is { } bytes ? $"Cache {Helpers.BytesToString(bytes)}" : "Cache empty";

        var mods = _modListService?.Mods;
        VersionsText = mods is null ? "mod list not loaded" : $"{mods.Sum(m => m.Versions.Count):N0} versions";

        if (_syncStatus is not null) (SyncText, SyncHint) = SyncTexts(_syncStatus, DateTimeOffset.Now);
    }

    public static (string Text, string Hint) SyncTexts(SyncStatusService sync, DateTimeOffset now)
    {
        var status = sync.Status;
        var last = status.LastFetch is { } fetch
            ? $"Last successful fetch: {fetch.LocalDateTime:yyyy-MM-dd HH:mm}" +
              (status.LastCommit is { Length: >= 7 } commit ? $" · {commit[..7]}" : "")
            : "CKAN-meta hasn't been fetched yet";

        if (sync.IsFetching) return ("fetching…", last);
        if (status.LastAttemptFailed) return ("sync failed", $"{status.LastError}\n{last}");
        return status.LastFetch is { } time ? ($"synced {SyncStatusService.TimeAgo(time, now)}", last) : ("synced –", last);
    }

    private static long? ReadCacheSize()
    {
        var folder = ModDownloader.DefaultCacheFolder;
        if (!Directory.Exists(folder)) return null;

        try
        {
            return Helpers.GetDirectorySize(folder);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
