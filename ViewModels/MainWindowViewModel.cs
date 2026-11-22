using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KMRLauncherMvvm.Data;
using KMRLauncherMvvm.Factories;
using KMRLauncherMvvm.Models;
using KMRLauncherMvvm.Services;
using KMRLauncherMvvm.Services.Api;

namespace KMRLauncherMvvm.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly PageFactory _pageFactory;
    private readonly IModApiService _api;
    private readonly GameSessionService _session;
    private readonly InstanceService _instances;
    private readonly DispatcherTimer _runningTimer;
    private DateTime _runningSince;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HomePageIsActive))]
    [NotifyPropertyChangedFor(nameof(DiscoverPageIsActive))]
    [NotifyPropertyChangedFor(nameof(InstancesPageIsActive))]
    [NotifyPropertyChangedFor(nameof(SettingsPageIsActive))]
    [NotifyPropertyChangedFor(nameof(PageTitle))]
    [NotifyPropertyChangedFor(nameof(CanSearch))]
    [NotifyPropertyChangedFor(nameof(SearchHint))]
    [NotifyPropertyChangedFor(nameof(ShowFetchButton))]
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
    public string? SearchHint => CanSearch ? null : "Search works on Discover for now";

    // The "Fetch CKAN-meta now" button: in the mockups only on Home and Discover
    public bool ShowFetchButton => HomePageIsActive || DiscoverPageIsActive;
    [ObservableProperty] [NotifyCanExecuteChangedFor(nameof(FetchCkanMetaCommand))]
    private bool _isFetching;

    // Sidebar: instance count and the primary instance with its Launch button
    [ObservableProperty] private int _instanceCount;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasPrimaryInstance))] [NotifyCanExecuteChangedFor(nameof(LaunchPrimaryCommand))]
    private Instance? _primaryInstance;
    public bool HasPrimaryInstance => PrimaryInstance is not null;

    [ObservableProperty] [NotifyCanExecuteChangedFor(nameof(LaunchPrimaryCommand))]
    private bool _isRunning;
    [ObservableProperty] private string _runningInstanceName = "";
    [ObservableProperty] private string _runningTime = "00:00:00";
    [ObservableProperty] private string? _launchError;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasSettingsProblem))]
    private string? _settingsProblem;

    private string? _dismissedSettingsProblem;
    public bool HasSettingsProblem => SettingsProblem is not null;

    public ObservableCollection<DialogHost> Dialogs { get; } = [];

    private void SyncDialogs(DialogService dialogs)
    {
        foreach (var host in Dialogs.Where(h => !dialogs.Open.Contains(h.Content)).ToList())
        {
            host.Detach();
            Dialogs.Remove(host);
        }

        foreach (var dialog in dialogs.Open.Where(d => Dialogs.All(h => h.Content != d)))
            Dialogs.Add(new DialogHost(dialog));
    }

    public StatusBarViewModel StatusBar { get; }

    public MainWindowViewModel(PageFactory pageFactory, StatusBarViewModel statusBar, IModApiService api,
        GameSessionService session, NavigationService navigation, DialogService dialogs, InstanceService instances)
    {
        dialogs.Changed += () => SyncDialogs(dialogs);
        _pageFactory = pageFactory;
        _api = api;
        _session = session;
        session.Changed += OnSessionChanged;
        navigation.NavigationRequested += (page, parameter) =>
        {
            ShowPage(page);
            (CurrentPage as INavigationTarget)?.OnNavigatedTo(parameter);
        };
        StatusBar = statusBar;

        _runningTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _runningTimer.Tick += (_, _) => RunningTime = (DateTime.Now - _runningSince).ToString(@"hh\:mm\:ss");

        _instances = instances;
        LoadInstances();
        instances.Changed += LoadInstances;

        GoToHome();
    }

    partial void OnCurrentPageChanged(PageViewModel? value) => SearchText = "";

    partial void OnSearchTextChanged(string value) => (CurrentPage as ISearchablePage)?.ApplySearch(value);

    private void LoadInstances()
    {
        var settings = _instances.Settings;
        InstanceCount = settings.Instances.Count;
        PrimaryInstance = _instances.Primary;
        if (settings.LoadProblem != _dismissedSettingsProblem) SettingsProblem = settings.LoadProblem;
    }

    [RelayCommand]
    private void DismissSettingsProblem()
    {
        _dismissedSettingsProblem = SettingsProblem;
        SettingsProblem = null;
    }

    [RelayCommand]
    private void GoToHome() => ShowPage(ApplicationPageNames.Home);

    [RelayCommand]
    private void GoToDiscover() => ShowPage(ApplicationPageNames.Discover);

    [RelayCommand]
    private void GoToInstances() => ShowPage(ApplicationPageNames.Instances);

    [RelayCommand]
    private void GoToSettings() => ShowPage(ApplicationPageNames.Settings);

    public ObservableCollection<PageViewModel> Pages { get; } = [];

    public async Task WarmUpPagesAsync()
    {
        List<PageViewModel> warming = [];
        foreach (var name in (ApplicationPageNames[])[ApplicationPageNames.Discover, ApplicationPageNames.Instances])
        {
            await Idle();
            if (Pages.Any(p => p.PageName == name)) continue;

            var page = _pageFactory.GetPageViewModel(name);
            page.IsWarming = true;
            Pages.Add(page);
            warming.Add(page);
        }

        foreach (var page in warming)
        {
            while (page is ILoadingPage { IsLoading: true }) await Task.Delay(100);
            await Idle();
            page.IsWarming = false;
        }
    }

    private static async Task Idle() => await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);

    private void ShowPage(ApplicationPageNames name)
    {
        var page = Pages.FirstOrDefault(p => p.PageName == name);
        if (page is null) Pages.Add(page = _pageFactory.GetPageViewModel(name));
        if (CurrentPage is { } previous) previous.IsActive = false;
        page.IsWarming = false;
        page.IsActive = true;
        CurrentPage = page;
        _ = StatusBar.RefreshAsync();
    }

    private bool CanLaunchPrimary() => PrimaryInstance is not null && !IsRunning;

    [RelayCommand]
    private void StopGame() => _session.Stop();

    [RelayCommand(CanExecute = nameof(CanLaunchPrimary))]
    private async Task LaunchPrimary()
    {
        if (PrimaryInstance is not { } instance) return;

        LaunchError = null;
        try
        {
            await _session.LaunchAsync(instance);
        }
        catch (Exception e)
        {
            LaunchError = $"Couldn't launch {instance.Name}: {e.Message}";
        }
    }

    private void OnSessionChanged()
    {
        IsRunning = _session.IsRunning;
        if (_session.Running is { } running)
        {
            RunningInstanceName = running.Name;
            RunningTime = "00:00:00";
            _runningSince = _session.StartedAt;
            _runningTimer.Start();
        }
        else
        {
            _runningTimer.Stop();
        }
    }

    private bool CanFetch() => !IsFetching;

    [RelayCommand(CanExecute = nameof(CanFetch))]
    private async Task FetchCkanMeta()
    {
        IsFetching = true;
        try
        {
            if (CurrentPage is DiscoverPageViewModel discover)
                await discover.FetchModsCommand.ExecuteAsync(true);
            else
                await Task.Run(() => _api.GetAllModsAsync(null, true));
        }
        catch (Exception)
        {
        }
        finally
        {
            IsFetching = false;
        }
    }
}
