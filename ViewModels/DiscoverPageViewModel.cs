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
    private readonly IModApiService _api;
    private bool _isLoading;
    private ObservableCollection<Mod> _modList = [];
    
    [ObservableProperty] private ObservableCollection<Mod> _modListFiltered = [];
    [ObservableProperty] private string _connectionStatus = "ARCHIVE // CONNECTING TO CKAN...";
    [ObservableProperty] private string _modFilter = "";
    
    partial void OnModFilterChanged(string value)
    {
        if (!_isLoading)
        {
            ApplyFilters();
        }
    }
    
    [ObservableProperty] private string _authorFilter = "";
    
    partial void OnAuthorFilterChanged(string value)
    {
        if (!_isLoading)
        {
            ApplyFilters();
        }
    }

    public DiscoverPageViewModel(IModApiService api)
    {
        _api = api;
        PageName = ApplicationPageNames.Discover;
        _ = FetchMods();
    }

    public DiscoverPageViewModel()
    {
    }

    [RelayCommand]
    private async Task FetchMods()
    {
        _isLoading = true;
        var mods = await Task.Run(() => _api.GetAllModsAsync());
        ConnectionStatus = "ARCHIVE // ACQUIRED CKAN DATA FEED";
        _modList = ModListFiltered = new ObservableCollection<Mod>(mods);
        _isLoading = false;
        ApplyFilters();
    }

    [RelayCommand]
    private void ApplyFilters()
    {
        var nameFilter = ModFilter.Trim();
        var authorFilter = AuthorFilter.Trim();

        var filtered = _modList.Where(mod =>
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