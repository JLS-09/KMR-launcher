using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
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
    [ObservableProperty] private ObservableCollection<ModListItemViewModel> _modListFiltered = [];
    [ObservableProperty] private string _connectionStatus = "ARCHIVE // CONNECTING TO CKAN...";
    [ObservableProperty] private string _modFilter = "";
    
    public ObservableCollection<ModListItemViewModel> SelectedMods { get; } = [];
    public bool HasSelectedMods => SelectedMods.Count > 0;
    
    partial void OnModFilterChanged(string value)
    {
        if (!IsLoading) ApplyFilters();
    }
    
    [ObservableProperty] private string _authorFilter = "";
    
    partial void OnAuthorFilterChanged(string value)
    {
        if (!IsLoading) ApplyFilters();
    }

    public DiscoverPageViewModel(IModApiService api, ModListService modListService)
    {
        _api = api;
        ModListService = modListService;
        PageName = ApplicationPageNames.Discover;
        SelectedMods.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasSelectedMods));
        if (_modListService.Mods is null) _ = FetchMods();
        else
        {
            ModListFiltered = ToItemViewModels(ModListService.Mods ?? []);
            ConnectionStatus = "ARCHIVE // ACQUIRED CKAN DATA FEED";
        }
    }

    public DiscoverPageViewModel() { }

    [RelayCommand]
    private async Task FetchMods(bool isRefresh = false)
    {
        LoadProgress = new ModFetchProgress { ModsReceived = 0, TotalMods = 1 };
        IsLoading = true;
        var progress = new Progress<ModFetchProgress>(pct => LoadProgress = pct);
        await Task.Run(() => _api.GetAllModsAsync(progress, isRefresh));
        ConnectionStatus = "ARCHIVE // ACQUIRED CKAN DATA FEED";
        ModListFiltered = ToItemViewModels(ModListService.Mods ?? []);
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

        var previousSelections = SelectedMods.ToDictionary(m => m.Mod.Id);
        
        ModListFiltered = new ObservableCollection<ModListItemViewModel>(
            filtered.Select(mod =>
            {
                if (previousSelections.TryGetValue(mod.Id, out var existing))
                    return existing;

                return new ModListItemViewModel(mod);
            }));
    }
    
    [RelayCommand]
    private void ToggleModSelection(ModListItemViewModel item)
    {
        item.IsSelected = !item.IsSelected;

        if (item.IsSelected)
            SelectedMods.Add(item);
        else
            SelectedMods.Remove(item);
    }
    
    private static ObservableCollection<ModListItemViewModel> ToItemViewModels(ObservableCollection<Mod> mods) =>
        new(mods.Select(m => new ModListItemViewModel(m)));
    
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
    
    [RelayCommand]
    private async Task ApplyChanges()
    {
        var json = JsonSerializer.Serialize(SelectedMods);
        Console.WriteLine(json);
    }
}