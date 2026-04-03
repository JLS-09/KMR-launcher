using System;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KMRLauncherMvvm.Data;
using KMRLauncherMvvm.Factories;
using KMRLauncherMvvm.Services.Api;

namespace KMRLauncherMvvm.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private PageFactory _pageFactory;
    
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HomePageIsActive))]
    [NotifyPropertyChangedFor(nameof(DiscoverPageIsActive))]
    [NotifyPropertyChangedFor(nameof(InstancesPageIsActive))]
    [NotifyPropertyChangedFor(nameof(SettingsPageIsActive))]
    private PageViewModel _currentPage;

    public bool HomePageIsActive => CurrentPage.PageName == ApplicationPageNames.Home;
    public bool DiscoverPageIsActive => CurrentPage.PageName == ApplicationPageNames.Discover;
    public bool InstancesPageIsActive => CurrentPage.PageName == ApplicationPageNames.Instances;
    public bool SettingsPageIsActive => CurrentPage.PageName == ApplicationPageNames.Settings;

    public MainWindowViewModel(PageFactory pageFactory)
    {
        _pageFactory = pageFactory;
        GoToHome();
    }

    public MainWindowViewModel()
    {
    }

    [RelayCommand]
    private void GoToHome()
    {
        CurrentPage = _pageFactory.GetPageViewModel(ApplicationPageNames.Home);
    }
    
    [RelayCommand]
    private async Task GoToDiscover()
    {
        CurrentPage = _pageFactory.GetPageViewModel(ApplicationPageNames.Discover);
    }
    
    [RelayCommand]
    private void GoToInstances()
    {
        CurrentPage = _pageFactory.GetPageViewModel(ApplicationPageNames.Instances);
    }
    
    [RelayCommand]
    private void GoToSettings()
    {
        CurrentPage = _pageFactory.GetPageViewModel(ApplicationPageNames.Settings);
    }
}