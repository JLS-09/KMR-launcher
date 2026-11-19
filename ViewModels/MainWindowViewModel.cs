using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KMRLauncherMvvm.Data;
using KMRLauncherMvvm.Factories;
using KMRLauncherMvvm.Models;
using KMRLauncherMvvm.Services;
using KMRLauncherMvvm.Views;

namespace KMRLauncherMvvm.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly PageFactory? _pageFactory;
    private readonly DispatcherTimer? _runningTimer;
    private DateTime _runningSince;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HomePageIsActive))]
    [NotifyPropertyChangedFor(nameof(DiscoverPageIsActive))]
    [NotifyPropertyChangedFor(nameof(InstancesPageIsActive))]
    [NotifyPropertyChangedFor(nameof(SettingsPageIsActive))]
    [NotifyPropertyChangedFor(nameof(PageTitle))]
    [NotifyPropertyChangedFor(nameof(CanSearch))]
    [NotifyPropertyChangedFor(nameof(SearchPlaceholder))]
    private PageViewModel? _currentPage;

    public bool HomePageIsActive => CurrentPage?.PageName == ApplicationPageNames.Home;
    public bool DiscoverPageIsActive => CurrentPage?.PageName == ApplicationPageNames.Discover;
    public bool InstancesPageIsActive => CurrentPage?.PageName == ApplicationPageNames.Instances;
    public bool SettingsPageIsActive => CurrentPage?.PageName == ApplicationPageNames.Settings;

    public string PageTitle => CurrentPage?.PageName switch
    {
        ApplicationPageNames.Home => "Home",
        ApplicationPageNames.Discover => "Discover",
        ApplicationPageNames.Instances => "Instances",
        ApplicationPageNames.Settings => "Settings",
        _ => ""
    };

    // Top bar search: only searches the current page for now
    [ObservableProperty] private string _searchText = "";
    public bool CanSearch => CurrentPage is ISearchablePage;
    public string SearchPlaceholder => (CurrentPage as ISearchablePage)?.SearchPlaceholder ?? "Search (not on this page yet)";

    // Sidebar: instance count and the primary instance with its Launch button
    [ObservableProperty] private int _instanceCount;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasPrimaryInstance))] [NotifyCanExecuteChangedFor(nameof(LaunchPrimaryCommand))]
    private Instance? _primaryInstance;
    public bool HasPrimaryInstance => PrimaryInstance is not null;

    // While a game started from the sidebar runs. The full Running card (Log, Stop, also for games started
    // on the Instances page) is Phase 5 task 5.
    [ObservableProperty] [NotifyCanExecuteChangedFor(nameof(LaunchPrimaryCommand))]
    private bool _isRunning;
    [ObservableProperty] private string _runningInstanceName = "";
    [ObservableProperty] private string _runningTime = "00:00:00";
    [ObservableProperty] private string? _launchError;

    public StatusBarViewModel StatusBar { get; }

    public MainWindowViewModel(PageFactory pageFactory, StatusBarViewModel statusBar)
    {
        _pageFactory = pageFactory;
        StatusBar = statusBar;

        _runningTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _runningTimer.Tick += (_, _) => RunningTime = (DateTime.Now - _runningSince).ToString(@"hh\:mm\:ss");

        LoadInstances(App.Settings);
        SettingsService.SettingsChanged += (_, settings) => LoadInstances(settings);

        GoToHome();
    }

    public MainWindowViewModel()
    {
        StatusBar = new StatusBarViewModel();
    }

    partial void OnCurrentPageChanged(PageViewModel? value) => SearchText = "";

    partial void OnSearchTextChanged(string value) => (CurrentPage as ISearchablePage)?.ApplySearch(value);

    private void LoadInstances(AppSettings settings)
    {
        InstanceCount = settings.Instances.Count;
        PrimaryInstance = settings.Instances.FirstOrDefault(i => i.IsPrimary) ?? settings.Instances.FirstOrDefault();
    }

    [RelayCommand]
    private void GoToHome() => ShowPage(ApplicationPageNames.Home);

    [RelayCommand]
    private void GoToDiscover() => ShowPage(ApplicationPageNames.Discover);

    [RelayCommand]
    private void GoToInstances() => ShowPage(ApplicationPageNames.Instances);

    [RelayCommand]
    private void GoToSettings() => ShowPage(ApplicationPageNames.Settings);

    private void ShowPage(ApplicationPageNames name)
    {
        if (_pageFactory is null) return;
        CurrentPage = _pageFactory.GetPageViewModel(name);
        _ = StatusBar.RefreshAsync();
    }

    private bool CanLaunchPrimary() => PrimaryInstance is not null && !IsRunning;

    [RelayCommand(CanExecute = nameof(CanLaunchPrimary))]
    private async Task LaunchPrimary()
    {
        if (PrimaryInstance is not { } instance) return;

        LaunchError = null;
        RunningInstanceName = instance.Name;
        RunningTime = "00:00:00";
        _runningSince = DateTime.Now;
        IsRunning = true;
        _runningTimer?.Start();

        try
        {
            await instance.LaunchInstanceWithWine();
        }
        catch (Exception e)
        {
            // e.g. wine isn't installed at /usr/bin/wine
            LaunchError = $"Couldn't launch {instance.Name}: {e.Message}";
        }
        finally
        {
            _runningTimer?.Stop();
            IsRunning = false;
        }
    }

    [RelayCommand]
    private void OpenPrimaryInstanceSettings()
    {
        if (PrimaryInstance is not { } instance) return;
        new CompatibleKspVersionsWindow { DataContext = new CompatibleKspVersionsViewModel(instance) }.Show();
    }

    [RelayCommand]
    private void OpenPrimaryInstanceFolder()
    {
        if (PrimaryInstance is not { } instance) return;

        try
        {
            Process.Start(new ProcessStartInfo("xdg-open") { ArgumentList = { instance.RootPath } });
        }
        catch (Exception e)
        {
            LaunchError = $"Couldn't open the folder: {e.Message}";
        }
    }
}
