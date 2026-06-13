using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KMRLauncherMvvm.Data;
using KMRLauncherMvvm.Models;
using KMRLauncherMvvm.Services.Api;
using KMRLauncherMvvm.ViewModels.InstallMods;
using KMRLauncherMvvm.Views.InstallMods;

namespace KMRLauncherMvvm.ViewModels;

public partial class DiscoverPageViewModel : PageViewModel
{
    public bool IsEnabledFlag => true;
    private readonly IModApiService _api;
    
    [ObservableProperty] private ModListService _modListService;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private ModFetchProgress _loadProgress;
    [ObservableProperty] private ObservableCollection<Mod> _modListFiltered = [];
    [ObservableProperty] private string _connectionStatus = "ARCHIVE // CONNECTING TO CKAN...";
    [ObservableProperty] private string _modFilter = "";
    
    partial void OnModFilterChanged(string value)
    {
        if (!IsLoading)
        {
            ApplyFilters();
        }
    }
    
    [ObservableProperty] private string _authorFilter = "";
    
    partial void OnAuthorFilterChanged(string value)
    {
        if (!IsLoading)
        {
            ApplyFilters();
        }
    }

    public DiscoverPageViewModel(IModApiService api, ModListService modListService)
    {
        _api = api;
        ModListService = modListService;
        PageName = ApplicationPageNames.Discover;
        if (_modListService.Mods is null)
        {
            _ = FetchMods();
        }
        else
        {
            ModListFiltered = ModListService.Mods ?? [];
            ConnectionStatus = "ARCHIVE // ACQUIRED CKAN DATA FEED";
        }
    }

    public DiscoverPageViewModel()
    {
    }

    [RelayCommand]
    private async Task FetchMods(bool isRefresh = false)
    {
        LoadProgress = new ModFetchProgress
        {
            ModsReceived = 0,
            TotalMods = 1
        };
        IsLoading = true;
        var progress = new Progress<ModFetchProgress>(pct => LoadProgress = pct);
        await Task.Run(() => _api.GetAllModsAsync(progress, isRefresh));
        ConnectionStatus = "ARCHIVE // ACQUIRED CKAN DATA FEED";
        ModListFiltered = ModListService.Mods ?? [];
        IsLoading = false;
        ApplyFilters();
    }

    [RelayCommand]
    private void ApplyFilters()
    {
        var nameFilter = ModFilter.Trim();
        var authorFilter = AuthorFilter.Trim();

        var filtered = (ModListService.Mods ?? []).Where(mod =>
            (nameFilter.IsWhiteSpace() || mod.Name.Contains(nameFilter, StringComparison.OrdinalIgnoreCase)) &&
            (authorFilter.IsWhiteSpace() || mod.AuthorsDisplay.Contains(authorFilter, StringComparison.OrdinalIgnoreCase)));

        ModListFiltered = new ObservableCollection<Mod>(filtered);
    }
    
    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task InstallMod(Mod mod)
    {
        var versionList = mod.Versions;
        
        var window = new InstallModsWindow
        {
            DataContext = new InstallModsViewModel(versionList)
        };
        window.Show();
    }
}