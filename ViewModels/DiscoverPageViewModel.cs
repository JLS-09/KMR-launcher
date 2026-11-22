using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KMRLauncherMvvm.Data;
using KMRLauncherMvvm.Models;
using KMRLauncherMvvm.Services;
using KMRLauncherMvvm.Services.Api;
using KMRLauncherMvvm.ViewModels.Discover;
using KMRLauncherMvvm.ViewModels.InstallMods;

namespace KMRLauncherMvvm.ViewModels;

public partial class DiscoverPageViewModel : PageViewModel, ISearchablePage, INavigationTarget, ILoadingPage
{
    private const int TagsShownCollapsed = 11;

    private readonly IModApiService _api;
    private readonly InstallFlow _installFlow;
    private readonly ModListService _modListService;
    private readonly InstanceService _instances;

    private List<ModListItemViewModel> _allRows = [];
    private Dictionary<string, int> _neededBy = [];
    private GameVersionCriteria? _criteria;
    private string _search = "";
    private bool _applyingFilters;
    private bool _loadingInstances;
    private int _loads;
    private int _rowsJob;
    private int _fitsJob;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private ModFetchProgress _loadProgress = new() { TotalMods = 1 };

    // The list
    [ObservableProperty] private ObservableCollection<ModListItemViewModel> _rows = [];
    [ObservableProperty] private string _shownCount = "–";
    [ObservableProperty] private bool _hasNoRows;

    // Filters column
    public ObservableCollection<Instance> Instances { get; } = [];
    [ObservableProperty] private Instance? _checkAgainst;
    [ObservableProperty] private bool _onlyCompatible;
    [ObservableProperty] private bool _hideInstalled;
    [ObservableProperty] private IReadOnlyList<FilterOptionViewModel> _tags = [];
    [ObservableProperty] private IReadOnlyList<FilterOptionViewModel> _visibleTags = [];
    [ObservableProperty] private bool _showAllTags;
    [ObservableProperty] private string _moreTagsText = "";
    [ObservableProperty] private bool _hasMoreTags;
    public IReadOnlyList<FilterOptionViewModel> Kinds { get; }
    [ObservableProperty] private IReadOnlyList<string> _activeFilters = [];
    [ObservableProperty] private bool _hasActiveFilters;

    // Details panel
    [ObservableProperty] private ModListItemViewModel? _focusedRow;
    [ObservableProperty] private ModDetailsViewModel? _details;

    // Selection tray
    public ObservableCollection<ModListItemViewModel> SelectedMods { get; } = [];
    [ObservableProperty] private bool _hasSelectedMods;
    [ObservableProperty] private Instance? _installTarget;

    public DiscoverPageViewModel(IModApiService api, ModListService modListService, InstallFlow installFlow,
        InstanceService instances)
    {
        PageName = ApplicationPageNames.Discover;
        SelectedMods.CollectionChanged += (_, _) => HasSelectedMods = SelectedMods.Count > 0;
        Kinds =
        [
            new FilterOptionViewModel(DiscoverFilter.PackageKind, 0, ApplyFilters),
            new FilterOptionViewModel(DiscoverFilter.MetapackageKind, 0, ApplyFilters)
        ];
        _api = api;
        _modListService = modListService;
        _installFlow = installFlow;
        _instances = instances;

        LoadInstances();
        instances.Changed += LoadInstances;

        if (modListService.Mods is null) _ = FetchMods();
        else _ = ShowMods(modListService.Mods);
    }

    public void ApplySearch(string text)
    {
        _search = text.Trim();
        ApplyFilters();
    }

    [RelayCommand]
    private async Task FetchMods(bool isRefresh = false)
    {
        LoadProgress = new ModFetchProgress { ModsReceived = 0, TotalMods = 1 };
        BeginLoading();
        try
        {
            var progress = new Progress<ModFetchProgress>(p => LoadProgress = p);
            var cacheOnly = !isRefresh && !_instances.Settings.FetchOnStartup;
            await Task.Run(() => _api.GetAllModsAsync(progress, isRefresh, cacheOnly));
            await LoadRows(_modListService.Mods ?? []);
        }
        finally
        {
            EndLoading();
        }
    }

    private async Task ShowMods(IEnumerable<Mod> mods)
    {
        BeginLoading();
        try
        {
            await LoadRows(mods);
        }
        finally
        {
            EndLoading();
        }
    }

    private void BeginLoading() => IsLoading = ++_loads > 0;
    private void EndLoading() => IsLoading = --_loads > 0;
    partial void OnIsLoadingChanged(bool value) => HasNoRows = Rows.Count == 0 && !value;

    private void LoadInstances()
    {
        var checkPath = CheckAgainst?.RootPath;
        var targetPath = InstallTarget?.RootPath;

        _loadingInstances = true;
        try
        {
            Instances.Clear();
            foreach (var instance in _instances.Usable) Instances.Add(instance);

            var primary = _instances.Primary;
            CheckAgainst = Instances.FirstOrDefault(i => i.RootPath == checkPath) ?? primary;
            InstallTarget = Instances.FirstOrDefault(i => i.RootPath == targetPath) ?? CheckAgainst;
        }
        finally
        {
            _loadingInstances = false;
        }

        _ = RefreshFits();
    }

    private async Task LoadRows(IEnumerable<Mod> mods)
    {
        var job = ++_rowsJob;
        var fitsJob = _fitsJob;
        var selected = SelectedMods.ToDictionary(r => r.Mod.Id);
        var (criteria, fitFor) = FitCheck();

        var built = await Task.Run(() => BuildRows(mods, selected, fitFor));
        if (job != _rowsJob) return;

        foreach (var row in built.Rows)
            if (!selected.TryGetValue(row.Mod.Id, out var kept) || kept != row)
                row.SelectionChanged += OnRowSelectionChanged;

        _allRows = built.Rows;
        _neededBy = built.NeededBy;
        _ = LoadDownloadCounts();

        Tags = [.. built.Tags.Select(t => new FilterOptionViewModel(t.Name, t.Count, ApplyFilters))];
        UpdateVisibleTags();

        ApplyFits(built.Fits, criteria);
        if (fitsJob != _fitsJob) _ = RefreshFits();
    }

    private static (List<ModListItemViewModel> Rows, ModFit[] Fits, Dictionary<string, int> NeededBy,
        List<(string Name, int Count)> Tags) BuildRows(IEnumerable<Mod> mods,
            Dictionary<string, ModListItemViewModel> selected, Func<Mod, ModFit> fitFor)
    {
        var modList = mods.Where(m => m.Versions.Count > 0).ToList();
        List<ModListItemViewModel> rows =
        [
            .. modList
                .OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
                .Select(m => selected.GetValueOrDefault(m.Id) ?? new ModListItemViewModel(m))
        ];
        List<(string, int)> tags =
        [
            .. modList
                .SelectMany(m => m.Tags ?? [])
                .GroupBy(t => t)
                .OrderByDescending(g => g.Count())
                .ThenBy(g => g.Key)
                .Select(g => (g.Key, g.Count()))
        ];
        return (rows, [.. rows.Select(r => fitFor(r.Mod))], DiscoverFilter.CountNeededBy(modList), tags);
    }

    private async Task LoadDownloadCounts()
    {
        var counts = await Task.Run(() => GitHelper.ReadDownloadCounts(GitHelper.DefaultCkanMetaFolder));
        foreach (var row in _allRows)
            row.Downloads = DiscoverFilter.FormatDownloads(counts.TryGetValue(row.Mod.Id, out var c) ? c : null);
    }

    private void OnRowSelectionChanged(ModListItemViewModel row)
    {
        if (row.IsSelected && !SelectedMods.Contains(row)) SelectedMods.Add(row);
        else if (!row.IsSelected) SelectedMods.Remove(row);
    }

    partial void OnCheckAgainstChanged(Instance? value)
    {
        if (!_loadingInstances) _ = RefreshFits();
    }
    partial void OnOnlyCompatibleChanged(bool value) => ApplyFilters();
    partial void OnHideInstalledChanged(bool value) => ApplyFilters();
    partial void OnShowAllTagsChanged(bool value) => UpdateVisibleTags();

    private (GameVersionCriteria? Criteria, Func<Mod, ModFit> For) FitCheck()
    {
        var criteria = CheckAgainst is null ? null : GameVersionCriteria.FromInstance(CheckAgainst);
        return (criteria, ModFit.Checker(CheckAgainst, criteria));
    }

    private async Task RefreshFits()
    {
        var job = ++_fitsJob;
        var rows = _allRows;
        var (criteria, fitFor) = FitCheck();

        var fits = rows.Count == 0 ? [] : await Task.Run(() => rows.Select(r => fitFor(r.Mod)).ToArray());
        if (job != _fitsJob || rows != _allRows) return;
        ApplyFits(fits, criteria);
    }

    private void ApplyFits(ModFit[] fits, GameVersionCriteria? criteria)
    {
        _criteria = criteria;
        for (var i = 0; i < _allRows.Count; i++) _allRows[i].Fit = fits[i];

        ApplyFilters();
        ShowDetails(FocusedRow);
    }

    private void ApplyFilters()
    {
        if (_applyingFilters) return;

        var tags = Tags.Where(t => t.IsChecked).Select(t => t.Name).ToHashSet();
        var kinds = Kinds.Where(k => k.IsChecked).Select(k => k.Name).ToHashSet();

        Rows = new ObservableCollection<ModListItemViewModel>(_allRows.Where(r =>
            DiscoverFilter.Matches(r.Mod, r.Fit, _search, tags, kinds, OnlyCompatible, HideInstalled)));
        ShownCount = Rows.Count.ToString("N0");
        HasNoRows = Rows.Count == 0 && !IsLoading;

        ActiveFilters = [.. tags.Select(t => $"tag: {t}"), .. kinds.Select(k => $"kind: {k}")];
        HasActiveFilters = ActiveFilters.Count > 0;
    }

    private void UpdateVisibleTags()
    {
        var hidden = Math.Max(0, Tags.Count - TagsShownCollapsed);
        HasMoreTags = hidden > 0;
        VisibleTags = ShowAllTags
            ? Tags
            : [.. Tags.Where((t, i) => i < TagsShownCollapsed || t.IsChecked)];
        MoreTagsText = ShowAllTags ? "Show less" : $"Show {hidden} more";
    }

    [RelayCommand]
    private void ToggleMoreTags() => ShowAllTags = !ShowAllTags;

    [RelayCommand]
    private void ClearFilters()
    {
        _applyingFilters = true;
        foreach (var option in Tags.Concat(Kinds)) option.IsChecked = false;
        OnlyCompatible = false;
        HideInstalled = false;
        _applyingFilters = false;

        ApplyFilters();
        UpdateVisibleTags();
    }

    partial void OnFocusedRowChanged(ModListItemViewModel? value) => ShowDetails(value);

    private void ShowDetails(ModListItemViewModel? row)
    {
        Details?.Detach();
        Details = row is null
            ? null
            : new ModDetailsViewModel(row, CheckAgainst, _criteria, _neededBy.GetValueOrDefault(row.Mod.Id));
    }

    [RelayCommand]
    private void CloseDetails() => FocusedRow = null;

    [RelayCommand]
    private void Deselect(ModListItemViewModel row) => row.IsSelected = false;

    [RelayCommand]
    private void ClearSelection()
    {
        foreach (var row in SelectedMods.ToList()) row.IsSelected = false;
    }

    [RelayCommand]
    private async Task ApplyChanges()
    {
        var installed = await _installFlow.InstallAsync(
            [
                .. SelectedMods.Select(m => new PlannedModVersion
                    { Version = m.SelectedVersion, IsUserRequested = true })
            ],
            InstallTarget);
        if (installed) ClearSelection();
    }

    public void OnNavigatedTo(object? parameter)
    {
        if (parameter is not Instance instance) return;

        var match = Instances.FirstOrDefault(i => i.RootPath == instance.RootPath);
        if (match is null) return;
        CheckAgainst = match;
        InstallTarget = match;
    }
}