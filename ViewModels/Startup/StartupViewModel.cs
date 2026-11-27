using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KMRLauncherMvvm.Models;
using KMRLauncherMvvm.Services;
using KMRLauncherMvvm.Services.Api;

namespace KMRLauncherMvvm.ViewModels.Startup;

public partial class StartupViewModel : ViewModelBase
{
    private const double SettingsEnd = 0.05;
    private const double FetchEnd = 0.55;
    private const double ReadEnd = 0.9;

    private readonly InstanceService _instances;
    private readonly IModApiService _api;
    private readonly ModListService _modList;
    private readonly HomeDataService _homeData;
    private readonly CancellationTokenSource _skip = new();
    private long _receivedBytes;
    private bool _loaded;

    public StartupStepViewModel Settings { get; } = new("Settings and instances loaded");
    public StartupStepViewModel Fetch { get; } = new("Fetching CKAN-meta");
    public StartupStepViewModel Read { get; } = new("Reading the mod list");
    public StartupStepViewModel Check { get; } = new("Checking your instances for updates");
    public IReadOnlyList<StartupStepViewModel> Steps => [Settings, Fetch, Read, Check];

    [ObservableProperty] private double _progress;
    [ObservableProperty] [NotifyCanExecuteChangedFor(nameof(SkipCommand))]
    private bool _canSkip;

    public string VersionText => AppInfo.VersionText;

    public string SourceText { get; } =
        $"{GitHelper.CkanMetaUrl.Replace("https://", "").Replace(".git", "")} · {GitHelper.CkanMetaBranch}";

    public StartupViewModel(InstanceService instances, IModApiService api, ModListService modList,
        HomeDataService homeData)
    {
        _instances = instances;
        _api = api;
        _modList = modList;
        _homeData = homeData;
    }

    public async Task RunAsync()
    {
        var count = _instances.Instances.Count;
        Settings.Finish(count == 1 ? "1 instance" : $"{count} instances");
        Progress = SettingsEnd;

        var cacheOnly = !_instances.Settings.FetchOnStartup && _api.HasCache;
        if (cacheOnly)
        {
            Fetch.Finish("Skipped", StartupStepState.Skipped);
            Progress = FetchEnd;
        }
        else
        {
            Fetch.Start();
            CanSkip = _api.HasCache;
        }

        await LoadModsAsync(cacheOnly);
        _loaded = true;
        CanSkip = false;
        FinishFetch();

        var mods = _modList.Mods;
        if (mods is null)
        {
            Read.Finish("No mod list yet", StartupStepState.Failed);
            Check.Finish("Skipped", StartupStepState.Skipped);
            Progress = 1;
            return;
        }

        Read.Finish($"{mods.Count.ToString("N0", CultureInfo.InvariantCulture)} mods");
        Progress = ReadEnd;

        Check.Start();
        var data = await Task.Run(_homeData.Recheck);
        var updates = data.Instances.Sum(i => i.Updates.Count + i.Replacements.Count);
        Check.Finish(updates switch
        {
            0 => "No updates",
            1 => "1 update",
            _ => $"{updates} updates"
        });
        Progress = 1;
    }

    private async Task LoadModsAsync(bool cacheOnly)
    {
        var progress = new Progress<ModFetchProgress>(OnProgress);
        try
        {
            await Task.Run(() => _api.GetAllModsAsync(progress, false, cacheOnly, _skip.Token));
            return;
        }
        catch (Exception)
        {
            Fetch.Finish("Couldn't fetch, using the last sync", StartupStepState.Failed);
        }

        if (!_api.HasCache) return;

        try
        {
            await Task.Run(() => _api.GetAllModsAsync(progress, false, true));
        }
        catch (Exception)
        {
        }
    }

    private bool CanSkipFetch() => CanSkip;

    [RelayCommand(CanExecute = nameof(CanSkipFetch))]
    private void Skip()
    {
        _skip.Cancel();
        CanSkip = false;
        if (Fetch.IsCurrent) Fetch.Detail = "Skipping…";
    }

    private void FinishFetch()
    {
        if (!Fetch.IsCurrent) return;

        if (_skip.IsCancellationRequested) Fetch.Finish("Skipped", StartupStepState.Skipped);
        else Fetch.Finish(_receivedBytes > 0 ? InstalledMods.FormatSize(_receivedBytes) : "Up to date");
    }

    private void OnProgress(ModFetchProgress p)
    {
        if (_loaded) return;

        var fraction = p.TotalMods > 0 ? Math.Clamp((double)p.ModsReceived / p.TotalMods, 0, 1) : 0;
        var percent = (int)(fraction * 100);

        if (p.Phase != FetchPhase.Reading)
        {
            if (!Fetch.IsCurrent || _skip.IsCancellationRequested) return;

            _receivedBytes = Math.Max(_receivedBytes, p.ReceivedBytes);
            var (start, end, detail) = p.Phase switch
            {
                FetchPhase.Receiving => (0.0, 0.7, $"{InstalledMods.FormatSize(_receivedBytes)} · {percent}%"),
                FetchPhase.Indexing => (0.7, 0.9, $"Indexing · {percent}%"),
                _ => (0.9, 1.0, $"Checking out · {percent}%")
            };
            Fetch.Detail = detail;
            Progress = SettingsEnd + (FetchEnd - SettingsEnd) * (start + (end - start) * fraction);
            return;
        }

        FinishFetch();
        if (Read.IsPending) Read.Start();
        if (!Read.IsCurrent) return;

        Read.Detail = p.IsCache
            ? "From cache"
            : $"{p.ModsReceived.ToString("N0", CultureInfo.InvariantCulture)} / {p.TotalMods.ToString("N0", CultureInfo.InvariantCulture)} files";
        Progress = FetchEnd + (ReadEnd - FetchEnd) * (p.IsCache ? 0 : fraction);
    }
}
