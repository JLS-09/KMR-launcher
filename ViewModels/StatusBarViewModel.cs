using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using KMRLauncherMvvm.Models;
using KMRLauncherMvvm.Services;
using LibGit2Sharp;

namespace KMRLauncherMvvm.ViewModels;

// The status bar at the bottom of the main window (Phase 9 task 3)
public partial class StatusBarViewModel : ObservableObject
{
    private readonly ModListService? _modListService;

    [ObservableProperty] private string _repoText = "not cloned yet";
    [ObservableProperty] private string _versionsText = "mod list not loaded";
    [ObservableProperty] private string _cacheText = "Cache –";

    // Placeholder until the startup work saves the sync time (Phase 6 task 1)
    [ObservableProperty] private string _syncText = "synced –";
    [ObservableProperty] private string _syncHint = "Not built yet: the time of the last sync is saved from Phase 6 on";

    public StatusBarViewModel(ModListService modListService)
    {
        _modListService = modListService;
        _modListService.ModsChanged += () => Dispatcher.UIThread.Post(() => _ = RefreshAsync());
        _ = RefreshAsync();
    }

    public StatusBarViewModel()
    {
    }

    public void ShowSyncFailed(string reason)
    {
        SyncText = "sync failed";
        SyncHint = reason;
    }

    public async Task RefreshAsync()
    {
        var (repo, cacheBytes) = await Task.Run(() => (ReadRepo(), ReadCacheSize()));

        RepoText = repo ?? "not cloned yet";
        CacheText = cacheBytes is { } bytes ? $"Cache {Helpers.BytesToString(bytes)}" : "Cache empty";

        var mods = _modListService?.Mods;
        VersionsText = mods is null ? "mod list not loaded" : $"{mods.Sum(m => m.Versions.Count):N0} versions";
    }

    // "master @ 7d01e4b"
    private static string? ReadRepo()
    {
        try
        {
            var folder = GitHelper.DefaultCkanMetaFolder;
            if (!Repository.IsValid(folder)) return null;

            using var repo = new Repository(folder);
            var branch = repo.Branches["master"] ?? repo.Head;
            return branch.Tip is null ? null : $"{branch.FriendlyName} @ {branch.Tip.Sha[..7]}";
        }
        catch (Exception)
        {
            return null;
        }
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
