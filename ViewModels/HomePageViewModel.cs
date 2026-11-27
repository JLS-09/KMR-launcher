using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KMRLauncherMvvm.Data;
using KMRLauncherMvvm.Models;
using KMRLauncherMvvm.Services;
using KMRLauncherMvvm.ViewModels.Home;
using KMRLauncherMvvm.ViewModels.InstallMods;
using KMRLauncherMvvm.ViewModels.NewInstance;

namespace KMRLauncherMvvm.ViewModels;

public partial class HomePageViewModel : PageViewModel
{
    private readonly InstanceService _instances;
    private readonly HomeDataService _homeData;
    private readonly GameSessionService _session;
    private readonly NavigationService _navigation;
    private readonly NewInstanceFlow _newInstanceFlow;
    private readonly DownloadCache _cache;
    private readonly StatusBarViewModel _statusBar;
    private readonly ModListService _modList;
    private readonly InstallFlow _installFlow;
    private const int FeedRowsCollapsed = 8;
    private int _sizesJob;
    private Dictionary<string, (long Total, long GameData)> _sizes = [];

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasContinue))]
    private HomeContinue? _continue;

    public bool HasContinue => Continue is not null;

    [ObservableProperty] private IReadOnlyList<HomeInstanceCard> _cards = [];
    [ObservableProperty] private string _instanceCount = "0";

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasNoAttention))]
    private IReadOnlyList<AttentionRow> _attention = [];

    public bool HasNoAttention => Attention.Count == 0;
    [ObservableProperty] private string _attentionCount = "0";
    [ObservableProperty] private string? _attentionError;

    [ObservableProperty] private HomeCkanFeed _feed = HomeCkanFeed.Empty;
    [ObservableProperty] private FeedTab _feedTab = FeedTab.YourMods;
    [ObservableProperty] private bool _showAllFeedRows;
    [ObservableProperty] private IReadOnlyList<FeedRow> _feedRows = [];
    [ObservableProperty] private string _feedEmptyText = "";
    [ObservableProperty] private string _showAllFeedText = "";
    [ObservableProperty] private bool _canShowAllFeedRows;
    [ObservableProperty] private string _updateAllText = "";
    [ObservableProperty] private bool _canUpdateAll;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasNoPopular))]
    private IReadOnlyList<PopularRow> _popular = [];

    public bool HasNoPopular => Popular.Count == 0;
    [ObservableProperty] private string _popularTitle = "";
    [ObservableProperty] private string _popularSubtitle = "";

    [ObservableProperty] private string _installSize = "–";
    [ObservableProperty] private string _gameDataSize = "";
    [ObservableProperty] private string? _actionError;

    [ObservableProperty] [NotifyCanExecuteChangedFor(nameof(LaunchCommand), nameof(LaunchInstanceCommand))]
    private bool _isGameRunning;

    public HomePageViewModel(InstanceService instances, HomeDataService homeData, GameSessionService session,
        NavigationService navigation, NewInstanceFlow newInstanceFlow, DownloadCache cache,
        StatusBarViewModel statusBar, ModListService modList, InstallFlow installFlow)
    {
        PageName = ApplicationPageNames.Home;
        _instances = instances;
        _homeData = homeData;
        _session = session;
        _navigation = navigation;
        _newInstanceFlow = newInstanceFlow;
        _cache = cache;
        _statusBar = statusBar;
        _modList = modList;
        _installFlow = installFlow;

        IsGameRunning = session.IsRunning;
        session.Changed += () => IsGameRunning = _session.IsRunning;
        homeData.Changed += () => Dispatcher.UIThread.Post(Refresh);
        Refresh();
    }

    private void Refresh()
    {
        var previous = Continue?.Instance.RootPath;
        var usable = _instances.Usable.ToList();
        var instance = HomeContinue.Pick(usable, _instances.Primary);
        Continue = instance is null ? null : new HomeContinue(instance, _homeData.For(instance));
        Cards = HomeInstanceCard.For(usable);
        InstanceCount = Cards.Count.ToString();
        Attention = HomeAttention.Rows([.. Cards.Select(c => c.Instance)], _homeData.Data);
        AttentionCount = Attention.Count.ToString();
        var mods = _modList.Mods?.ToDictionary(m => m.Id) ?? [];
        Feed = HomeCkanFeed.Build(_homeData.Data.CkanChanges, mods, [.. Cards.Select(c => c.Instance)],
            _homeData.Data, DateTimeOffset.Now);
        ShowFeedRows();
        var primary = _instances.Primary;
        Popular = PopularRow.For(primary, _homeData.Data, mods);
        PopularTitle = primary is null ? "Popular" : $"Popular for KSP {primary.Version}";
        PopularSubtitle = primary is null ? "" : $"CKAN download counts · not in {primary.Name} yet";
        if (instance?.RootPath != previous) ActionError = null;

        ShowSizes();
        _ = LoadSizes(usable);
    }

    private async Task LoadSizes(List<Instance> instances)
    {
        var job = ++_sizesJob;
        var measured = await Task.Run(() => instances.ToDictionary(i => i.RootPath,
            i => (Helpers.FolderSize(i.RootPath), Helpers.FolderSize(Path.Combine(i.RootPath, "GameData")))));
        if (job != _sizesJob) return;

        _sizes = measured;
        ShowSizes();
    }

    private void ShowSizes()
    {
        foreach (var card in Cards)
            if (_sizes.TryGetValue(card.Instance.RootPath, out var size))
                card.SizeText = InstalledMods.FormatSize(size.Total);

        if (Continue is not null && _sizes.TryGetValue(Continue.Instance.RootPath, out var sizes))
        {
            InstallSize = InstalledMods.FormatSize(sizes.Total);
            GameDataSize = $"GameData {InstalledMods.FormatSize(sizes.GameData)}";
        }
        else
        {
            InstallSize = "–";
            GameDataSize = "";
        }
    }

    private bool CanLaunch() => !IsGameRunning;

    [RelayCommand(CanExecute = nameof(CanLaunch))]
    private Task Launch() => Continue?.Instance is { } instance ? LaunchAsync(instance) : Task.CompletedTask;

    [RelayCommand(CanExecute = nameof(CanLaunch))]
    private Task LaunchInstance(Instance instance) => LaunchAsync(instance);

    private async Task LaunchAsync(Instance instance)
    {
        ActionError = null;
        try
        {
            await _session.LaunchAsync(instance);
        }
        catch (Exception e)
        {
            ActionError = $"Couldn't launch {instance.Name}: {e.Message}";
        }
    }

    [RelayCommand]
    private void OpenFolder()
    {
        if (Continue?.Instance is not { } instance) return;

        try
        {
            Helpers.Open(instance.RootPath);
        }
        catch (Exception e)
        {
            ActionError = $"Couldn't open the folder: {e.Message}";
        }
    }

    [RelayCommand]
    private void ManageMods() => OpenInstance(InstanceTab.Mods, ModsFilter.All);

    [RelayCommand]
    private void Review() => OpenInstance(InstanceTab.Mods, Continue?.ReviewFilter ?? ModsFilter.All);

    [RelayCommand]
    private void LaunchArguments() => OpenInstance(InstanceTab.Settings, null);

    [RelayCommand]
    private Task NewInstance() => _newInstanceFlow.CreateAsync();

    [RelayCommand]
    private void ManageInstances() => _navigation.NavigateTo(ApplicationPageNames.Instances);

    [RelayCommand]
    private async Task AttentionAction(AttentionRow row)
    {
        AttentionError = null;
        if (row.Target is { } target)
        {
            _navigation.NavigateTo(ApplicationPageNames.Instances, new InstanceNavigation(target, InstanceTab.Mods,
                row.Filter));
            return;
        }

        var instances = _instances.Instances.ToList();
        var result = await Task.Run(() => _cache.RemoveUnused(instances));
        if (result.Failed > 0)
            AttentionError = $"{result.Failed} {(result.Failed == 1 ? "archive" : "archives")} couldn't be deleted.";
        await _statusBar.RefreshAsync();
    }

    partial void OnFeedTabChanged(FeedTab value)
    {
        ShowAllFeedRows = false;
        ShowFeedRows();
    }

    private void ShowFeedRows()
    {
        var rows = Feed.Rows(FeedTab);
        FeedRows = ShowAllFeedRows ? rows : [.. rows.Take(FeedRowsCollapsed)];
        CanShowAllFeedRows = !ShowAllFeedRows && rows.Count > FeedRowsCollapsed;
        ShowAllFeedText = $"Show all {rows.Count}";
        FeedEmptyText = FeedTab switch
        {
            FeedTab.YourMods => "No new versions of your mods in this sync",
            FeedTab.AllUpdates => "No new versions in this sync",
            _ => "No new mods in this sync"
        };

        var updates = Feed.YourMods.SelectMany(r => r.Updates).ToList();
        var instances = updates.Select(u => u.Instance).Distinct().Count();
        CanUpdateAll = FeedTab == FeedTab.YourMods && updates.Count > 0;
        UpdateAllText = $"Update all {Feed.YourMods.Count} in {instances} {(instances == 1 ? "instance" : "instances")}";
    }

    [RelayCommand]
    private void SelectFeedTab(FeedTab tab) => FeedTab = tab;

    [RelayCommand]
    private void ShowAllFeed()
    {
        ShowAllFeedRows = true;
        ShowFeedRows();
    }

    [RelayCommand]
    private Task UpdateFeedRow(FeedRow row) => RunUpdates(row.Updates);

    [RelayCommand]
    private Task UpdateAllFeed() => RunUpdates([.. Feed.YourMods.SelectMany(r => r.Updates)]);

    private async Task RunUpdates(IReadOnlyList<FeedUpdate> updates)
    {
        foreach (var group in updates.GroupBy(u => u.Instance))
        {
            var requested = group.Select(u => InstalledMods.PlanUpdate(u.Instance, u.Installed, u.Target)).ToList();
            if (!await _installFlow.InstallAsync(requested, group.Key, InstallSource.Updates)) break;
        }
    }

    [RelayCommand]
    private void AddPopular(PopularRow row) => _navigation.NavigateTo(ApplicationPageNames.Discover, row.Mod);

    [RelayCommand]
    private void BrowseMods() => _navigation.NavigateTo(ApplicationPageNames.Discover);

    private void OpenInstance(InstanceTab tab, ModsFilter? filter)
    {
        if (Continue?.Instance is { } instance)
            _navigation.NavigateTo(ApplicationPageNames.Instances, new InstanceNavigation(instance, tab, filter));
    }
}
