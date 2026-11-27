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

public record InstanceNavigation(Instance Instance, InstanceTab Tab = InstanceTab.Mods,
    ModsFilter? Filter = null);

public partial class InstancesPageViewModel : PageViewModel, ISearchablePage, INavigationTarget, ILoadingPage
{
    private readonly NewInstanceFlow _newInstanceFlow;
    private readonly ModListService _modListService;
    private readonly GameSessionService _session;
    private readonly NavigationService _navigation;
    private readonly InstanceService _instanceService;

    [ObservableProperty] private IReadOnlyList<InstanceListItemViewModel> _instances = [];
    [ObservableProperty] private InstanceListItemViewModel? _selectedItem;
    [ObservableProperty] private string _instancesCountText = "0 INSTANCES";
    [ObservableProperty] private bool _sortByName;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(ShowInstanceDetails), nameof(ShowMissing))]
    private bool _hasInstances;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(ShowInstanceDetails), nameof(ShowMissing))]
    private bool _selectedIsMissing;

    public bool ShowInstanceDetails => HasInstances && !SelectedIsMissing;
    public bool ShowMissing => HasInstances && SelectedIsMissing;

    [ObservableProperty] private Instance? _selected;
    [ObservableProperty] private InstanceSummary? _summary;
    public InstalledModsViewModel Mods { get; }

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(DeleteText))]
    private string _installSize = "–";

    [ObservableProperty] private string _gameDataSize = "";

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(ShowSelectionBar), nameof(ShowSettingsBar))]
    private InstanceTab _tab = InstanceTab.Mods;

    [ObservableProperty] [NotifyCanExecuteChangedFor(nameof(LaunchCommand), nameof(DeleteInstanceCommand), nameof(ForgetInstanceCommand))]
    private bool _isGameRunning;

    private readonly Dictionary<string, InstanceSettingsViewModel> _settingsByPath = [];
    [ObservableProperty] private InstanceSettingsViewModel? _instanceSettings;

    [ObservableProperty] private string? _actionError;


    public InstancesPageViewModel(NewInstanceFlow newInstanceFlow, ModListService modListService, GameSessionService session,
        NavigationService navigation, InstallFlow installFlow, InstanceService instances)
    {
        PageName = ApplicationPageNames.Instances;
        _newInstanceFlow = newInstanceFlow;
        _modListService = modListService;
        _session = session;
        _navigation = navigation;
        _instanceService = instances;
        Mods = new InstalledModsViewModel(modListService, installFlow);
        Mods.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(InstalledModsViewModel.HasSelection)) OnPropertyChanged(nameof(ShowSelectionBar));
        };

        IsGameRunning = session.IsRunning;
        session.Changed += OnSessionChanged;
        instances.Changed += LoadInstances;
        modListService.ModsChanged += OnModsChanged;
        LoadInstances();
    }

    public bool IsLoading => Mods.IsLoading;

    public void ApplySearch(string text) => Mods.ModFilterText = text;

    private void OnSessionChanged() => IsGameRunning = _session.IsRunning;
    private void OnModsChanged() => Dispatcher.UIThread.Post(() =>
    {
        if (Selected is { } selected) _ = Mods.Show(selected);
    });

    private void LoadInstances()
    {
        var all = _instanceService.Instances;
        var previous = Selected;
        var selectedPath = Selected?.RootPath;

        IEnumerable<Instance> ordered = SortByName
            ? all.OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
            : all.OrderByDescending(i => i.PlayTime > 0 ? i.LastPlayed : DateTime.MinValue)
                .ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase);
        Instances = [.. ordered.Select(i => new InstanceListItemViewModel(i))];
        InstancesCountText = $"{Instances.Count} INSTANCE{(Instances.Count == 1 ? "" : "S")}";
        HasInstances = Instances.Count > 0;

        SelectedItem = Instances.FirstOrDefault(i => i.Instance.RootPath == selectedPath)
                       ?? Instances.FirstOrDefault(i => i.IsPrimary)
                       ?? Instances.FirstOrDefault();

        if (Selected is { IsMissing: false } && ReferenceEquals(Selected, previous)) ShowDetails(Selected);

        foreach (var path in _settingsByPath.Keys.Where(p => all.All(i => i.RootPath != p)).ToList())
            _settingsByPath.Remove(path);
    }

    partial void OnSortByNameChanged(bool value) => LoadInstances();

    [RelayCommand]
    private void SortInstances(string by) => SortByName = by == "name";

    public void OnNavigatedTo(object? parameter)
    {
        var target = parameter switch
        {
            Instance instance => new InstanceNavigation(instance),
            InstanceNavigation navigation => navigation,
            _ => null
        };
        if (target is null) return;

        var item = Instances.FirstOrDefault(i => i.Instance.RootPath == target.Instance.RootPath);
        if (item is not null) SelectedItem = item;
        Tab = target.Tab;
        if (target.Filter is { } filter) Mods.Filter = filter;
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
            Tab = InstanceTab.Mods;
            return;
        }

        ShowDetails(value);
        _ = LoadSizes(value);
    }

    private void ShowDetails(Instance value)
    {
        Summary = new InstanceSummary(value);
        _ = Mods.Show(value);

        if (!_settingsByPath.TryGetValue(value.RootPath, out var settings) || !settings.HasChanges)
        {
            if (settings is not null) settings.PropertyChanged -= OnInstanceSettingsChanged;
            settings = new InstanceSettingsViewModel(value, _instanceService.Instances, i => _instanceService.Save(i));
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

    public bool ShowSelectionBar => Tab == InstanceTab.Mods && Mods.HasSelection;
    public bool ShowSettingsBar => Tab == InstanceTab.Settings && InstanceSettings is { HasChanges: true };

    private async Task LoadSizes(Instance instance)
    {
        InstallSize = "–";
        GameDataSize = "";

        var (total, gameData, modSizes) = await Task.Run(() =>
        {
            var sizes = instance.ModRecords.ToDictionary(r => r.Key, r => r.Value.Files.Sum(f =>
            {
                var file = new FileInfo(Path.Combine(instance.RootPath, f));
                return file.Exists ? file.Length : 0;
            }));
            return (Helpers.FolderSize(instance.RootPath), Helpers.FolderSize(Path.Combine(instance.RootPath, "GameData")),
                sizes);
        });

        if (Selected != instance) return;
        InstallSize = InstalledMods.FormatSize(total);
        GameDataSize = $"GameData {InstalledMods.FormatSize(gameData)}";
        Mods.ShowSizes(modSizes);
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
            Helpers.Open(Selected.RootPath);
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
            return _instanceService.OwnsPrefix(instance)
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
            await _instanceService.DeleteAsync(instance);
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
        if (!_instanceService.Instances.Contains(instance)) return;
        try
        {
            _instanceService.Forget(instance);
            Tab = InstanceTab.Mods;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            ActionError = $"Couldn't save the instance list: {e.Message}";
        }
    }

    [RelayCommand]
    private void MakePrimary()
    {
        if (Selected is { } selected) _instanceService.MakePrimary(selected);
    }

    [RelayCommand]
    private void RemoveFromList()
    {
        if (Selected is { IsMissing: true } missing) _instanceService.Forget(missing);
    }

    public void LocateFolder(string folder)
    {
        if (Selected is not { IsMissing: true } missing) return;

        ActionError = null;
        try
        {
            var (found, error) = _instanceService.Relocate(missing, folder);
            ActionError = error;
            if (found is null) return;

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