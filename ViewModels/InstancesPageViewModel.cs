using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KMRLauncherMvvm.Data;
using KMRLauncherMvvm.Models;
using KMRLauncherMvvm.Services;
using KMRLauncherMvvm.ViewModels.InstallMods;
using KMRLauncherMvvm.ViewModels.Instances;
using KMRLauncherMvvm.ViewModels.NewInstance;

namespace KMRLauncherMvvm.ViewModels;

public enum InstanceTab
{
    Mods,
    Settings,
    Logs,
    Backups
}

public enum ModsFilter
{
    All,
    Picked,
    Dependencies,
    Updates,
    Issues
}

public partial class InstancesPageViewModel : PageViewModel, ISearchablePage, INavigationTarget
{
    private readonly NewInstanceFlow _newInstanceFlow;
    private readonly ModListService _modListService;
    private readonly GameSessionService _session;
    private readonly NavigationService _navigation;
    private readonly InstallFlow _installFlow;

    private List<InstalledModRowViewModel> _allRows = [];

    [ObservableProperty] private IReadOnlyList<InstanceListItemViewModel> _instances = [];
    [ObservableProperty] private InstanceListItemViewModel? _selectedItem;
    [ObservableProperty] private string _instancesCountText = "0 INSTANCES";
    [ObservableProperty] private bool _sortByName;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(ShowInstanceDetails), nameof(ShowMissing))]
    private bool _hasInstances;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(ShowInstanceDetails), nameof(ShowMissing))]
    private bool _selectedIsMissing;

    [ObservableProperty] private string _missingReason = "";
    [ObservableProperty] private string _missingPath = "";

    public bool ShowInstanceDetails => HasInstances && !SelectedIsMissing;
    public bool ShowMissing => HasInstances && SelectedIsMissing;

    [ObservableProperty] private Instance? _selected;
    [ObservableProperty] private string _kspText = "";
    [ObservableProperty] private string _compatibleText = "";
    [ObservableProperty] private string _pathText = "";
    [ObservableProperty] private string _modsCount = "0";
    [ObservableProperty] private string _modsDetail = "";
    [ObservableProperty] private string _playTime = "";
    [ObservableProperty] private string _lastPlayed = "";
    [ObservableProperty] private string _lastPlayedDetail = "";

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(DeleteText))]
    private string _installSize = "–";

    [ObservableProperty] private string _gameDataSize = "";
    [ObservableProperty] private string _selectedName = "";
    [ObservableProperty] private bool _selectedIsPrimary;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(ShowSelectionBar), nameof(ShowSettingsBar))]
    private InstanceTab _tab = InstanceTab.Mods;

    [ObservableProperty] [NotifyCanExecuteChangedFor(nameof(LaunchCommand), nameof(DeleteInstanceCommand), nameof(ForgetInstanceCommand))]
    private bool _isGameRunning;

    private readonly Dictionary<string, InstanceSettingsViewModel> _settingsByPath = [];
    [ObservableProperty] private InstanceSettingsViewModel? _instanceSettings;

    [ObservableProperty] private string? _actionError;

    [ObservableProperty] private ObservableCollection<InstalledModRowViewModel> _rows = [];
    [ObservableProperty] private string _modFilterText = "";
    [ObservableProperty] private ModsFilter _filter = ModsFilter.All;
    [ObservableProperty] private string _allCount = "0";
    [ObservableProperty] private string _pickedCount = "0";
    [ObservableProperty] private string _dependenciesCount = "0";
    [ObservableProperty] private string _updatesCount = "0";
    [ObservableProperty] private string _issuesCount = "0";
    [ObservableProperty] private bool _hasUpdates;
    [ObservableProperty] private string _shownText = "";

    [ObservableProperty] private int _selectedCount;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(ShowSelectionBar))]
    private bool _hasSelection;

    [ObservableProperty] private string _removalNote = "";
    [ObservableProperty] private bool _selectionHasUpdates;

    public InstancesPageViewModel(NewInstanceFlow newInstanceFlow, ModListService modListService, GameSessionService session,
        NavigationService navigation, InstallFlow installFlow)
    {
        PageName = ApplicationPageNames.Instances;
        _newInstanceFlow = newInstanceFlow;
        _modListService = modListService;
        _session = session;
        _navigation = navigation;
        _installFlow = installFlow;

        IsGameRunning = session.IsRunning;
        session.Changed += OnSessionChanged;
        SettingsService.SettingsChanged += OnSettingsChanged;
        modListService.ModsChanged += OnModsChanged;
        LoadInstances(App.Settings);
    }

    public void ApplySearch(string text) => ModFilterText = text;

    private void OnSettingsChanged(object? sender, AppSettings settings) => LoadInstances(settings);
    private void OnSessionChanged() => IsGameRunning = _session.IsRunning;
    private void OnModsChanged() => Dispatcher.UIThread.Post(BuildRows);

    private void LoadInstances(AppSettings settings)
    {
        var previous = Selected;
        var selectedPath = Selected?.RootPath;

        IEnumerable<Instance> ordered = SortByName
            ? settings.Instances.OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
            : settings.Instances.OrderByDescending(i => i.PlayTime > 0 ? i.LastPlayed : DateTime.MinValue)
                .ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase);
        Instances = [.. ordered.Select(i => new InstanceListItemViewModel(i))];
        InstancesCountText = $"{Instances.Count} INSTANCE{(Instances.Count == 1 ? "" : "S")}";
        HasInstances = Instances.Count > 0;

        SelectedItem = Instances.FirstOrDefault(i => i.Instance.RootPath == selectedPath)
                       ?? Instances.FirstOrDefault(i => i.IsPrimary)
                       ?? Instances.FirstOrDefault();

        if (Selected is { IsMissing: false } && ReferenceEquals(Selected, previous)) ShowDetails(Selected);

        foreach (var path in _settingsByPath.Keys.Where(p => settings.Instances.All(i => i.RootPath != p)).ToList())
            _settingsByPath.Remove(path);
    }

    partial void OnSortByNameChanged(bool value) => LoadInstances(App.Settings);

    [RelayCommand]
    private void SortInstances(string by) => SortByName = by == "name";

    public void OnNavigatedTo(object? parameter)
    {
        if (parameter is not Instance instance) return;

        var item = Instances.FirstOrDefault(i => i.Instance.RootPath == instance.RootPath);
        if (item is not null) SelectedItem = item;
        Tab = InstanceTab.Mods;
    }

    partial void OnSelectedItemChanged(InstanceListItemViewModel? value)
    {
        if (value is null && Instances.Count > 0) return;
        Selected = value?.Instance;
    }

    partial void OnSelectedChanged(Instance? value)
    {
        ActionError = null;
        SelectedIsMissing = value?.IsMissing ?? false;
        if (value is null) return;

        if (value.IsMissing)
        {
            SelectedName = value.Name;
            MissingReason = value.LoadError ?? "";
            MissingPath = value.RootPath;
            Tab = InstanceTab.Mods;
            return;
        }

        ShowDetails(value);
        _ = LoadSizes(value);
    }

    private void ShowDetails(Instance value)
    {
        SelectedName = value.Name;
        SelectedIsPrimary = value.IsPrimary;
        KspText = $"KSP {value.Version}";
        CompatibleText = InstanceTexts.Compatible(value);
        PathText = InstanceTexts.ShortPath(value.RootPath);
        PlayTime = InstanceTexts.PlayTime(value);
        LastPlayed = InstanceTexts.LastPlayed(value);
        LastPlayedDetail = InstanceTexts.LastPlayedDetail(value);
        BuildRows();

        if (!_settingsByPath.TryGetValue(value.RootPath, out var settings) || !settings.HasChanges)
        {
            if (settings is not null) settings.PropertyChanged -= OnInstanceSettingsChanged;
            settings = new InstanceSettingsViewModel(value, App.Settings.Instances);
            settings.PropertyChanged += OnInstanceSettingsChanged;
            _settingsByPath[value.RootPath] = settings;
        }

        InstanceSettings = settings;
        OnPropertyChanged(nameof(DeleteText));
        OnPropertyChanged(nameof(ShowSettingsBar));
    }

    private void OnInstanceSettingsChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(InstanceSettingsViewModel.HasChanges)) OnPropertyChanged(nameof(ShowSettingsBar));
    }

    public bool ShowSelectionBar => Tab == InstanceTab.Mods && HasSelection;
    public bool ShowSettingsBar => Tab == InstanceTab.Settings && InstanceSettings is { HasChanges: true };

    private async Task LoadSizes(Instance instance)
    {
        InstallSize = "–";
        GameDataSize = "";

        var (total, gameData, modSizes) = await Task.Run(() =>
        {
            long Folder(string path) => Directory.Exists(path) ? Helpers.GetDirectorySize(path) : 0;
            var sizes = instance.ModRecords.ToDictionary(r => r.Key, r => r.Value.Files.Sum(f =>
            {
                var file = new FileInfo(Path.Combine(instance.RootPath, f));
                return file.Exists ? file.Length : 0;
            }));
            return (Folder(instance.RootPath), Folder(Path.Combine(instance.RootPath, "GameData")), sizes);
        });

        if (Selected != instance) return;
        InstallSize = InstalledMods.FormatSize(total);
        GameDataSize = $"GameData {InstalledMods.FormatSize(gameData)}";
        foreach (var row in _allRows)
            row.SizeText = modSizes.TryGetValue(row.Version.Identifier, out var size) && size > 0
                ? InstalledMods.FormatSize(size)
                : "–";
    }

    private void BuildRows()
    {
        if (Selected is not { } instance) return;

        UnmanagedDlls.Refresh(instance);

        var criteria = GameVersionCriteria.FromInstance(instance);
        var mods = _modListService.Mods?.ToDictionary(m => m.Id) ?? [];
        var sizes = _allRows.ToDictionary(r => r.Version.Identifier, r => r.SizeText);

        foreach (var row in _allRows) row.SelectionChanged -= OnRowSelectionChanged;
        _allRows =
        [
            .. instance.Mods
                .Select(v => new InstalledModRowViewModel(instance, v, mods.GetValueOrDefault(v.Identifier), criteria,
                    InstalledMods.FindReplacement(instance, v, mods, criteria)))
                .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
        ];
        foreach (var row in _allRows)
        {
            row.SizeText = sizes.GetValueOrDefault(row.Version.Identifier, "");
            row.SelectionChanged += OnRowSelectionChanged;
        }

        var picked = _allRows.Count(r => !r.IsDependency);
        var updates = _allRows.Count(r => r.HasUpdateOrReplacement);
        ModsCount = _allRows.Count.ToString();
        ModsDetail = $"{picked} picked · {_allRows.Count - picked} dependencies";
        AllCount = _allRows.Count.ToString();
        PickedCount = picked.ToString();
        DependenciesCount = (_allRows.Count - picked).ToString();
        UpdatesCount = updates.ToString();
        IssuesCount = _allRows.Count(r => r.IsNotListed).ToString();
        HasUpdates = updates > 0;

        ApplyModFilter();
        OnRowSelectionChanged();
    }

    partial void OnModFilterTextChanged(string value) => ApplyModFilter();
    partial void OnFilterChanged(ModsFilter value) => ApplyModFilter();

    [RelayCommand]
    private void SetFilter(ModsFilter filter) => Filter = filter;

    private void ApplyModFilter()
    {
        var text = ModFilterText.Trim();
        Rows = new ObservableCollection<InstalledModRowViewModel>(_allRows.Where(r =>
            (text.Length == 0 || r.Name.Contains(text, StringComparison.OrdinalIgnoreCase) ||
             r.Version.Identifier.Contains(text, StringComparison.OrdinalIgnoreCase)) &&
            Filter switch
            {
                ModsFilter.Picked => !r.IsDependency,
                ModsFilter.Dependencies => r.IsDependency,
                ModsFilter.Updates => r.HasUpdateOrReplacement,
                ModsFilter.Issues => r.IsNotListed,
                _ => true
            }));
        ShownText = $"Showing {Rows.Count} of {_allRows.Count}";
    }

    private void OnRowSelectionChanged()
    {
        var selected = _allRows.Where(r => r.IsSelected).ToList();
        SelectedCount = selected.Count;
        HasSelection = selected.Count > 0;
        SelectionHasUpdates = selected.Any(r => r.HasUpdateOrReplacement);
        RemovalNote = Selected is null || selected.Count == 0
            ? ""
            : InstalledMods.DescribeRemoval(InstalledMods.PlanRemoval(Selected,
                [.. selected.Select(r => r.Version.Identifier)]));
    }

    [RelayCommand]
    private void ClearSelection()
    {
        foreach (var row in _allRows) row.IsSelected = false;
    }

    [RelayCommand]
    private Task RemoveSelected() => Remove([.. _allRows.Where(r => r.IsSelected)]);

    [RelayCommand]
    private Task RemoveRow(InstalledModRowViewModel row) => Remove([row]);

    private async Task Remove(IReadOnlyList<InstalledModRowViewModel> rows)
    {
        if (Selected is not { } instance || rows.Count == 0) return;

        var plan = InstalledMods.PlanRemoval(instance, [.. rows.Select(r => r.Version.Identifier)]);
        await _installFlow.RemoveAsync(instance, plan.All);
    }

    [RelayCommand]
    private Task UpdateSelected() => Update([.. _allRows.Where(r => r.IsSelected && r.HasUpdateOrReplacement)]);

    [RelayCommand]
    private Task UpdateAll() => Update([.. _allRows.Where(r => r.HasUpdateOrReplacement)]);

    [RelayCommand]
    private Task UpdateRow(InstalledModRowViewModel row) => Update([row]);

    private async Task Update(IReadOnlyList<InstalledModRowViewModel> rows)
    {
        if (Selected is not { } instance || rows.Count == 0) return;

        var requested = rows.Select(r => new PlannedModVersion
        {
            Version = r.Replacement ?? r.NextVersion!,
            IsUserRequested = InstalledMods.IsUserRequested(instance, r.Version.Identifier),
            IsVersionPickedByUser = true,
            OptionalReason = instance.ModRecords.GetValueOrDefault(r.Version.Identifier)?.Reason,
            Replaces = r.HasReplacement ? r.Version.Identifier : null
        }).ToList();
        List<ModVersion> replaced = [.. rows.Where(r => r.HasReplacement).Select(r => r.Version)];

        if (await _installFlow.InstallAsync(requested, instance, InstallSource.Updates, replaced)) ClearSelection();
    }

    private bool CanLaunch() => !IsGameRunning;

    [RelayCommand(CanExecute = nameof(CanLaunch))]
    private async Task Launch()
    {
        if (Selected is not { } instance) return;

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
        if (Selected is null) return;

        try
        {
            Process.Start(new ProcessStartInfo(Selected.RootPath) { UseShellExecute = true });
        }
        catch (Exception e)
        {
            ActionError = $"Couldn't open the folder: {e.Message}";
        }
    }

    [RelayCommand]
    private void OpenSettings() => Tab = InstanceTab.Settings;

    public string DeleteText
    {
        get
        {
            if (Selected is not { } instance) return "";

            var size = InstallSize == "–" ? "" : $" ({InstallSize})";
            var ownPrefix = WinePrefixes.IsManaged(instance.PrefixPath) &&
                            !App.Settings.Instances.Any(i =>
                                i != instance && WinePrefixes.SamePath(i.PrefixPath, instance.PrefixPath));
            return ownPrefix
                ? $"Removes the folder, its saves and its prefix{size}. Cached downloads stay."
                : $"Removes the folder and its saves{size}. The prefix in " +
                  $"{InstanceTexts.ShortPath(instance.PrefixPath)} and cached downloads stay.";
        }
    }

    private bool CanDeleteInstance() => !IsGameRunning;

    [RelayCommand(CanExecute = nameof(CanDeleteInstance))]
    private async Task DeleteInstance()
    {
        if (Selected is not { } instance) return;

        ActionError = null;
        try
        {
            await instance.DeleteInstance(App.Settings);
            Tab = InstanceTab.Mods;
        }
        catch (Exception e)
        {
            ActionError = $"Couldn't delete {instance.Name}: {e.Message}";
        }
    }

    [RelayCommand(CanExecute = nameof(CanDeleteInstance))]
    private void ForgetInstance()
    {
        if (Selected is not { } instance) return;

        ActionError = null;
        var index = App.Settings.Instances.IndexOf(instance);
        if (index < 0) return;
        try
        {
            App.Settings.Instances.RemoveAt(index);
            SettingsService.Save(App.Settings);
            Tab = InstanceTab.Mods;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            App.Settings.Instances.Insert(index, instance);
            ActionError = $"Couldn't save the instance list: {e.Message}";
        }
    }

    [RelayCommand]
    private void MakePrimary()
    {
        if (Selected is null) return;
        foreach (var instance in App.Settings.Instances) instance.IsPrimary = instance == Selected;
        SettingsService.Save(App.Settings);
    }

    [RelayCommand]
    private void RemoveFromList()
    {
        if (Selected is not { IsMissing: true } missing) return;
        App.Settings.Instances.Remove(missing);
        SettingsService.Save(App.Settings);
    }

    public void LocateFolder(string folder)
    {
        if (Selected is not { IsMissing: true } missing) return;

        ActionError = null;
        try
        {
            var (found, error) = InstanceStore.Relocate(App.Settings, missing, folder);
            ActionError = error;
            if (found is null) return;

            SettingsService.Save(App.Settings);
            SelectedItem = Instances.FirstOrDefault(i => i.Instance == found) ?? SelectedItem;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            ActionError = $"Couldn't save the instance list: {e.Message}";
        }
    }

    [RelayCommand]
    private void NewInstance() => _newInstanceFlow.CreateAsync();

    [RelayCommand]
    private void ImportInstance() => _newInstanceFlow.CreateAsync(existingFolder: true);

    [RelayCommand]
    private void AddMods()
    {
        if (Selected is not null) _navigation.NavigateTo(ApplicationPageNames.Discover, Selected);
    }

    [RelayCommand]
    private void ShowTab(InstanceTab tab) => Tab = tab;
}