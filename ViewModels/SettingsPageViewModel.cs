using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KMRLauncherMvvm.Data;
using KMRLauncherMvvm.Models;
using KMRLauncherMvvm.Services;
using KMRLauncherMvvm.Services.Api;
using KMRLauncherMvvm.ViewModels.Instances;
using KMRLauncherMvvm.ViewModels.Settings;

namespace KMRLauncherMvvm.ViewModels;

public partial class SettingsPageViewModel : PageViewModel
{
    private readonly ZipService? _zipService;
    private readonly IModApiService? _api;
    private readonly SyncStatusService? _syncStatus;
    private readonly DownloadCache? _cache;
    private readonly StatusBarViewModel? _statusBar;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasNoZips))]
    private IReadOnlyList<KspZipRow> _kspZips = [];

    [ObservableProperty] private string? _addZipError;
    [ObservableProperty] private string _parallelDownloads = "";

    [ObservableProperty] private string _lastFetchText = "never";
    [ObservableProperty] private string _repoStateText = "Not fetched yet";
    [ObservableProperty] private string _repoStateHint = "";
    [ObservableProperty] private bool _isUpToDate;
    [ObservableProperty] private bool _isFetchFailed;
    [ObservableProperty] private string? _fetchError;
    [ObservableProperty] private string? _openError;
    [ObservableProperty] private bool _fetchOnStartup;

    [ObservableProperty] private string _inUseText = "–";
    [ObservableProperty] private string _unusedText = "–";
    [ObservableProperty] private string _archivesText = "";
    [ObservableProperty] private GridLength _inUseWidth = new(0, GridUnitType.Star);
    [ObservableProperty] private GridLength _unusedWidth = new(0, GridUnitType.Star);
    [ObservableProperty] private string _removeUnusedText = "";
    [ObservableProperty] private string _clearAllText = "";
    [ObservableProperty] private string? _cacheError;

    [ObservableProperty] private string _wineDebugText = "";
    [ObservableProperty] private string _accentName = "";

    [ObservableProperty] private string? _aboutError;

    public string VersionText => AppInfo.VersionText;
    public string SourceText => AppInfo.SourceUrl.Replace("https://", "");
    public string DataFolderText => InstanceTexts.ShortPath(AppInfo.DataFolder);

    public IReadOnlyList<AccentSwatch> AccentSwatches { get; } =
        [.. ThemeService.AccentChoices.Select(c => new AccentSwatch(c.Name, c.Hex))];

    public string WinePathText { get; } = WineLauncher.FindOnPath("wine") ?? "not found";
    public bool WineFound => WinePathText != "not found";
    public string UmuPathText { get; } = WineLauncher.FindOnPath("umu-run") ?? "not found";
    public bool UmuFound => UmuPathText != "not found";

    [ObservableProperty] [NotifyCanExecuteChangedFor(nameof(RemoveUnusedCommand))]
    private bool _hasUnused;

    [ObservableProperty] [NotifyCanExecuteChangedFor(nameof(ClearCacheCommand))]
    private bool _hasArchives;

    [ObservableProperty] [NotifyCanExecuteChangedFor(nameof(FetchNowCommand))]
    private bool _isFetching;

    public bool HasNoZips => KspZips.Count == 0;
    public string RepoUrlText => GitHelper.CkanMetaUrl.Replace("https://", "");
    public string BranchText => GitHelper.CkanMetaBranch;
    public string LocalCopyText => InstanceTexts.ShortPath(GitHelper.DefaultCkanMetaFolder);
    public string CacheFolderText => _cache is null ? "" : InstanceTexts.ShortPath(_cache.Folder);

    public SettingsPageViewModel(ZipService zipService, IModApiService api, SyncStatusService syncStatus,
        DownloadCache cache, StatusBarViewModel statusBar) : this()
    {
        _zipService = zipService;
        _api = api;
        _syncStatus = syncStatus;
        _cache = cache;
        _statusBar = statusBar;
        _ = MeasureCacheAsync();
        _syncStatus.Changed += () => Dispatcher.UIThread.Post(LoadRepoState);

        LoadZips();
        LoadRepoState();
        ParallelDownloads = App.Settings.MaxParallelDownloads.ToString(CultureInfo.InvariantCulture);
        FetchOnStartup = App.Settings.FetchOnStartup;
        WineDebugText = DescribeWineDebug(App.Settings.WineDebug);
        ShowAccent(App.Settings.AccentColor);
    }

    public SettingsPageViewModel()
    {
        PageName = ApplicationPageNames.Settings;
    }

    public void AddKspZip(string path)
    {
        AddZipError = null;
        var settings = App.Settings;
        var name = Path.GetFileName(path);

        if (settings.KspZips.Any(z => z.Path == path))
        {
            AddZipError = $"{name} is already in the list.";
            return;
        }

        var zip = _zipService?.GetVersionFromKspZip(path);
        if (zip is null || string.IsNullOrEmpty(zip.Version))
        {
            AddZipError = $"Couldn't read a known KSP version from {name}. Is it a KSP zip?";
            return;
        }

        settings.KspZips.Add(zip);
        SettingsService.Save(settings);
        LoadZips();
    }

    [RelayCommand]
    private void RemoveKspZip(KspZipRow row)
    {
        AddZipError = null;
        var settings = App.Settings;
        settings.KspZips.Remove(row.Zip);
        SettingsService.Save(settings);
        LoadZips();
    }

    private bool CanFetchNow() => !IsFetching;

    [RelayCommand(CanExecute = nameof(CanFetchNow))]
    private async Task FetchNow()
    {
        if (_api is null) return;

        try
        {
            await Task.Run(() => _api.GetAllModsAsync(null, true));
        }
        catch (Exception)
        {
        }
    }

    [RelayCommand]
    private void OpenLocalCopy()
    {
        try
        {
            Helpers.Open(GitHelper.DefaultCkanMetaFolder);
        }
        catch (Exception e)
        {
            OpenError = $"Couldn't open the folder: {e.Message}";
        }
    }

    [RelayCommand]
    private void OpenCacheFolder()
    {
        if (_cache is null) return;

        try
        {
            Directory.CreateDirectory(_cache.Folder);
            Helpers.Open(_cache.Folder);
        }
        catch (Exception e)
        {
            CacheError = $"Couldn't open the folder: {e.Message}";
        }
    }

    [RelayCommand(CanExecute = nameof(HasUnused))]
    private async Task RemoveUnused()
    {
        if (_cache is null) return;

        var instances = App.Settings.Instances.ToList();
        await Cleanup(() => _cache.RemoveUnused(instances));
    }

    [RelayCommand(CanExecute = nameof(HasArchives))]
    private async Task ClearCache()
    {
        if (_cache is null) return;

        await Cleanup(_cache.ClearAll);
    }

    private async Task Cleanup(Func<CacheCleanup> cleanup)
    {
        CacheError = null;
        var result = await Task.Run(cleanup);
        if (result.Failed > 0)
            CacheError = $"{result.Failed} {Plural(result.Failed, "archive")} couldn't be deleted.";

        await MeasureCacheAsync();
        if (_statusBar is not null) await _statusBar.RefreshAsync();
    }

    private async Task MeasureCacheAsync()
    {
        if (_cache is null) return;

        var instances = App.Settings.Instances.ToList();
        var usage = await Task.Run(() => _cache.Measure(instances));

        InUseText = InstalledMods.FormatSize(usage.InUseBytes);
        UnusedText = InstalledMods.FormatSize(usage.UnusedBytes);
        ArchivesText = $"{usage.ArchiveCount:N0} {Plural(usage.ArchiveCount, "archive")}";
        InUseWidth = new GridLength(usage.InUseBytes, GridUnitType.Star);
        UnusedWidth = new GridLength(usage.UnusedBytes, GridUnitType.Star);
        HasUnused = usage.UnusedCount > 0;
        HasArchives = usage.ArchiveCount > 0;
        RemoveUnusedText = $"Deletes {usage.UnusedCount:N0} {Plural(usage.UnusedCount, "archive")} " +
                           $"({UnusedText}) that no instance has installed. They're downloaded again if an " +
                           "install needs them.";
        ClearAllText = $"Deletes all {usage.ArchiveCount:N0} {Plural(usage.ArchiveCount, "archive")} " +
                       $"({InstalledMods.FormatSize(usage.InUseBytes + usage.UnusedBytes)}). Installed mods stay; " +
                       "they're downloaded again when you update or reinstall them.";
    }

    [RelayCommand]
    private void SetWineDebug(WineDebugLevel level)
    {
        var settings = App.Settings;
        WineDebugText = DescribeWineDebug(level);
        if (settings.WineDebug == level) return;

        settings.WineDebug = level;
        SettingsService.Save(settings);
    }

    [RelayCommand]
    private void PickAccent(AccentSwatch swatch)
    {
        var settings = App.Settings;
        ShowAccent(swatch.Hex);
        if (string.Equals(settings.AccentColor, swatch.Hex, StringComparison.OrdinalIgnoreCase)) return;

        settings.AccentColor = swatch.Hex;
        ThemeService.ApplyAccent(swatch.Hex);
        SettingsService.Save(settings);
    }

    private void ShowAccent(string hex)
    {
        var current = AccentSwatches.FirstOrDefault(s => string.Equals(s.Hex, hex, StringComparison.OrdinalIgnoreCase));
        foreach (var swatch in AccentSwatches) swatch.IsSelected = swatch == current;
        AccentName = current?.Name ?? hex;
    }

    [RelayCommand]
    private void OpenSource() => OpenForAbout(AppInfo.SourceUrl, "the link");

    [RelayCommand]
    private void OpenDataFolder() => OpenForAbout(AppInfo.DataFolder, "the folder");

    private void OpenForAbout(string target, string what)
    {
        AboutError = null;
        try
        {
            Helpers.Open(target);
        }
        catch (Exception e)
        {
            AboutError = $"Couldn't open {what}: {e.Message}";
        }
    }

    public static string DescribeWineDebug(WineDebugLevel level) => level switch
    {
        WineDebugLevel.Off => "Off",
        WineDebugLevel.ErrorsOnly => "Errors only",
        _ => "Errors and fixmes"
    };

    private static string Plural(int count, string word) => count == 1 ? word : word + "s";

    partial void OnFetchOnStartupChanged(bool value)
    {
        if (App.Settings is not { } settings || settings.FetchOnStartup == value) return;

        settings.FetchOnStartup = value;
        SettingsService.Save(settings);
    }

    partial void OnParallelDownloadsChanged(string value)
    {
        if (App.Settings is not { } settings) return;

        var downloads = int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
            ? Math.Clamp(number, 1, AppSettings.MaxAllowedParallelDownloads)
            : settings.MaxParallelDownloads;

        if (downloads != settings.MaxParallelDownloads)
        {
            settings.MaxParallelDownloads = downloads;
            SettingsService.Save(settings);
        }

        var text = downloads.ToString(CultureInfo.InvariantCulture);
        if (value != text) ParallelDownloads = text;
    }

    private void LoadRepoState()
    {
        if (_syncStatus is null) return;

        var status = _syncStatus.Status;
        var commit = status.LastCommit ?? GitHelper.ReadHeadSha(GitHelper.DefaultCkanMetaFolder);
        var shortCommit = commit is { Length: >= 7 } ? commit[..7] : null;
        LastFetchText = (status.LastFetch, shortCommit) switch
        {
            ({ } time, { } sha) => $"{SyncStatusService.TimeAgo(time, DateTimeOffset.Now)} · {sha}",
            ({ } time, null) => SyncStatusService.TimeAgo(time, DateTimeOffset.Now),
            (null, { } sha) => $"– · {sha}",
            _ => "never"
        };

        IsFetching = _syncStatus.IsFetching;
        IsFetchFailed = !IsFetching && status.LastAttemptFailed;
        IsUpToDate = !IsFetching && !IsFetchFailed && status.LastFetch is not null;
        (RepoStateText, RepoStateHint) = (IsFetching, IsFetchFailed, IsUpToDate) switch
        {
            (true, _, _) => ("Fetching…", "Fetching CKAN-meta"),
            (_, true, _) => ("Last fetch failed", status.LastError ?? ""),
            (_, _, true) => ("Up to date", $"As of the last fetch, {status.LastFetch!.Value.LocalDateTime:yyyy-MM-dd HH:mm}"),
            _ => ("Not fetched yet", "CKAN-meta hasn't been fetched yet")
        };
        FetchError = IsFetchFailed ? $"Last fetch failed: {status.LastError}" : null;
    }

    private void LoadZips()
    {
        KspZips =
        [
            .. App.Settings.KspZips
                .OrderByDescending(z => z.Version, Comparer<string>.Create(CkanVersion.Compare))
                .ThenBy(z => z.Path, StringComparer.Ordinal)
                .Select(z => new KspZipRow(z))
        ];
    }
}
