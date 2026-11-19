using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KMRLauncherMvvm.Data;
using KMRLauncherMvvm.Models;
using KMRLauncherMvvm.Services;
using KMRLauncherMvvm.Services.Api;
using KMRLauncherMvvm.ViewModels.Discover;
using KMRLauncherMvvm.ViewModels.InstallMods;
using KMRLauncherMvvm.Views.InstallMods;

namespace KMRLauncherMvvm.ViewModels;

public partial class DiscoverPageViewModel : PageViewModel, ISearchablePage
{
    private const int TagsShownCollapsed = 11;

    private readonly IModApiService? _api;
    private readonly InstallService? _installService;
    private readonly DependencyService? _dependencyService;
    private readonly CompatibilityService? _compatibilityService;
    private readonly ModListService? _modListService;

    private List<ModListItemViewModel> _allRows = [];
    private Dictionary<string, int> _neededBy = [];
    private GameVersionCriteria? _criteria;
    private string _search = "";
    private bool _applyingFilters;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private ModFetchProgress _loadProgress = new() { TotalMods = 1 };

    // The list
    [ObservableProperty] private ObservableCollection<ModListItemViewModel> _rows = [];
    [ObservableProperty] private string _shownCount = "0";
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

    public DiscoverPageViewModel(IModApiService api, ModListService modListService,
        DependencyService dependencyService, CompatibilityService compatibilityService, InstallService installService)
        : this()
    {
        _api = api;
        _modListService = modListService;
        _dependencyService = dependencyService;
        _compatibilityService = compatibilityService;
        _installService = installService;

        LoadInstances(App.Settings);
        SettingsService.SettingsChanged += (_, settings) => LoadInstances(settings);

        if (modListService.Mods is null) _ = FetchMods();
        else LoadRows(modListService.Mods);
    }

    public DiscoverPageViewModel()
    {
        PageName = ApplicationPageNames.Discover;
        SelectedMods.CollectionChanged += (_, _) => HasSelectedMods = SelectedMods.Count > 0;
        Kinds =
        [
            new FilterOptionViewModel(DiscoverFilter.PackageKind, 0, ApplyFilters),
            new FilterOptionViewModel(DiscoverFilter.MetapackageKind, 0, ApplyFilters)
        ];
    }

    // The top bar's search box
    public void ApplySearch(string text)
    {
        _search = text.Trim();
        ApplyFilters();
    }

    [RelayCommand]
    private async Task FetchMods(bool isRefresh = false)
    {
        if (_api is null || _modListService is null) return;

        LoadProgress = new ModFetchProgress { ModsReceived = 0, TotalMods = 1 };
        IsLoading = true;
        try
        {
            var progress = new Progress<ModFetchProgress>(p => LoadProgress = p);
            await Task.Run(() => _api.GetAllModsAsync(progress, isRefresh));
            LoadRows(_modListService.Mods ?? []);
        }
        finally
        {
            IsLoading = false;
        }
    }

    // Instances for "Check against" and "Install to". Settings are replaced after a launch or an install,
    // so the picked instances are looked up again by folder.
    private void LoadInstances(AppSettings settings)
    {
        var checkPath = CheckAgainst?.RootPath;
        var targetPath = InstallTarget?.RootPath;

        Instances.Clear();
        foreach (var instance in settings.Instances) Instances.Add(instance);

        var primary = settings.Instances.FirstOrDefault(i => i.IsPrimary) ?? settings.Instances.FirstOrDefault();
        CheckAgainst = settings.Instances.FirstOrDefault(i => i.RootPath == checkPath) ?? primary;
        InstallTarget = settings.Instances.FirstOrDefault(i => i.RootPath == targetPath) ?? CheckAgainst;

        // An install changes the instance's mods, so the statuses change too
        RefreshFits();
    }

    private void LoadRows(IEnumerable<Mod> mods)
    {
        var modList = mods.Where(m => m.Versions.Count > 0).ToList();
        var selected = SelectedMods.ToDictionary(r => r.Mod.Id);

        _allRows = [.. modList
            .OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
            .Select(m => selected.GetValueOrDefault(m.Id) ?? CreateRow(m))];
        _neededBy = DiscoverFilter.CountNeededBy(modList);
        _ = LoadDownloadCounts();

        Tags = [.. modList
            .SelectMany(m => m.Tags ?? [])
            .GroupBy(t => t)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key)
            .Select(g => new FilterOptionViewModel(g.Key, g.Count(), ApplyFilters))];
        UpdateVisibleTags();

        RefreshFits();
    }

    private async Task LoadDownloadCounts()
    {
        var counts = await Task.Run(() => GitHelper.ReadDownloadCounts(GitHelper.DefaultCkanMetaFolder));
        foreach (var row in _allRows)
            row.Downloads = DiscoverFilter.FormatDownloads(counts.TryGetValue(row.Mod.Id, out var c) ? c : null);
    }

    private ModListItemViewModel CreateRow(Mod mod)
    {
        var row = new ModListItemViewModel(mod);
        row.SelectionChanged += OnRowSelectionChanged;
        return row;
    }

    private void OnRowSelectionChanged(ModListItemViewModel row)
    {
        if (row.IsSelected && !SelectedMods.Contains(row)) SelectedMods.Add(row);
        else if (!row.IsSelected) SelectedMods.Remove(row);
    }

    partial void OnCheckAgainstChanged(Instance? value) => RefreshFits();
    partial void OnOnlyCompatibleChanged(bool value) => ApplyFilters();
    partial void OnHideInstalledChanged(bool value) => ApplyFilters();
    partial void OnShowAllTagsChanged(bool value) => UpdateVisibleTags();

    // Works out every row's status against the "Check against" instance
    private void RefreshFits()
    {
        _criteria = CheckAgainst is null ? null : GameVersionCriteria.FromInstance(CheckAgainst);
        foreach (var row in _allRows) row.Fit = ModFit.For(row.Mod, CheckAgainst, _criteria);

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
        // Checked tags stay visible when the list is collapsed
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
        if (_modListService is null || _dependencyService is null || _compatibilityService is null) return;

        var viewModel = new InstallModsViewModel(
            [.. SelectedMods.Select(m => new PlannedModVersion { Version = m.SelectedVersion, IsUserRequested = true })],
            _modListService, _dependencyService, _compatibilityService, InstallTarget);
        var window = new InstallModsWindow { DataContext = viewModel };

        viewModel.Cancelled += () => window.Close(null);
        viewModel.Finished += data => window.Close(data);

        if ((Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow is not
            { } owner)
        {
            window.Show();
            return;
        }

        var confirmed = await window.ShowDialog<InstallModsData?>(owner);
        if (confirmed?.SelectedInstance is not { } instance || _installService is null) return;

        var progress = new InstallProgressViewModel(_installService, InstallRun.Create(instance, confirmed),
            App.Settings.MaxParallelDownloads);
        var progressWindow = new InstallProgressWindow { DataContext = progress };
        progress.CloseRequested += () => progressWindow.Close();

        var shown = progressWindow.ShowDialog(owner);
        var succeeded = await progress.RunAsync();
        await shown;

        if (succeeded) ClearSelection();
    }
}
