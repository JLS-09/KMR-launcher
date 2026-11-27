using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KMRLauncherMvvm.Data;
using KMRLauncherMvvm.Models;
using KMRLauncherMvvm.Services;
using KMRLauncherMvvm.ViewModels.Home;
using KMRLauncherMvvm.ViewModels.NewInstance;

namespace KMRLauncherMvvm.ViewModels;

public partial class HomePageViewModel : PageViewModel
{
    private readonly InstanceService _instances;
    private readonly HomeDataService _homeData;
    private readonly GameSessionService _session;
    private readonly NavigationService _navigation;
    private readonly NewInstanceFlow _newInstanceFlow;
    private int _sizesJob;
    private string? _sizesFolder;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasContinue))]
    private HomeContinue? _continue;

    public bool HasContinue => Continue is not null;

    [ObservableProperty] private string _installSize = "–";
    [ObservableProperty] private string _gameDataSize = "";
    [ObservableProperty] private string? _actionError;

    [ObservableProperty] [NotifyCanExecuteChangedFor(nameof(LaunchCommand))]
    private bool _isGameRunning;

    public HomePageViewModel(InstanceService instances, HomeDataService homeData, GameSessionService session,
        NavigationService navigation, NewInstanceFlow newInstanceFlow)
    {
        PageName = ApplicationPageNames.Home;
        _instances = instances;
        _homeData = homeData;
        _session = session;
        _navigation = navigation;
        _newInstanceFlow = newInstanceFlow;

        IsGameRunning = session.IsRunning;
        session.Changed += () => IsGameRunning = _session.IsRunning;
        instances.Changed += Refresh;
        homeData.Changed += () => Dispatcher.UIThread.Post(Refresh);
        Refresh();
    }

    private void Refresh()
    {
        var previous = Continue?.Instance.RootPath;
        var instance = HomeContinue.Pick(_instances.Usable, _instances.Primary);
        Continue = instance is null ? null : new HomeContinue(instance, _homeData.For(instance));
        if (instance is null) return;

        if (instance.RootPath != previous) ActionError = null;
        _ = LoadSizes(instance);
    }

    private async Task LoadSizes(Instance instance)
    {
        var job = ++_sizesJob;
        if (instance.RootPath != _sizesFolder)
        {
            InstallSize = "–";
            GameDataSize = "";
        }

        var (total, gameData) = await Task.Run(() => (Helpers.FolderSize(instance.RootPath),
            Helpers.FolderSize(Path.Combine(instance.RootPath, "GameData"))));
        if (job != _sizesJob) return;

        _sizesFolder = instance.RootPath;
        InstallSize = InstalledMods.FormatSize(total);
        GameDataSize = $"GameData {InstalledMods.FormatSize(gameData)}";
    }

    private bool CanLaunch() => !IsGameRunning;

    [RelayCommand(CanExecute = nameof(CanLaunch))]
    private async Task Launch()
    {
        if (Continue?.Instance is not { } instance) return;

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

    private void OpenInstance(InstanceTab tab, ModsFilter? filter)
    {
        if (Continue?.Instance is { } instance)
            _navigation.NavigateTo(ApplicationPageNames.Instances, new InstanceNavigation(instance, tab, filter));
    }
}
