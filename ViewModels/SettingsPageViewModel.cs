using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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

    [ObservableProperty] [NotifyCanExecuteChangedFor(nameof(FetchNowCommand))]
    private bool _isFetching;

    public bool HasNoZips => KspZips.Count == 0;
    public string RepoUrlText => GitHelper.CkanMetaUrl.Replace("https://", "");
    public string BranchText => GitHelper.CkanMetaBranch;
    public string LocalCopyText => InstanceTexts.ShortPath(GitHelper.DefaultCkanMetaFolder);

    public SettingsPageViewModel(ZipService zipService, IModApiService api, SyncStatusService syncStatus) : this()
    {
        _zipService = zipService;
        _api = api;
        _syncStatus = syncStatus;
        _syncStatus.Changed += () => Dispatcher.UIThread.Post(LoadRepoState);

        LoadZips();
        LoadRepoState();
        ParallelDownloads = App.Settings.MaxParallelDownloads.ToString(CultureInfo.InvariantCulture);
        FetchOnStartup = App.Settings.FetchOnStartup;
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
            Helpers.OpenFolder(GitHelper.DefaultCkanMetaFolder);
        }
        catch (Exception e)
        {
            OpenError = $"Couldn't open the folder: {e.Message}";
        }
    }

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
