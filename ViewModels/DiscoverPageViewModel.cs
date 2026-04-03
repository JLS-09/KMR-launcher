using System;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using KMRLauncherMvvm.Data;
using KMRLauncherMvvm.Models;
using KMRLauncherMvvm.Services.Api;

namespace KMRLauncherMvvm.ViewModels;

public partial class DiscoverPageViewModel : PageViewModel
{
    private readonly IModApiService _api;

    public ObservableCollection<Mod> Mods
    {
        get;
        set { if (field != value) { field = value; OnPropertyChanged(); }}
    }

    public string ConnectionStatus
    {
        get;
        set { if (field != value) { field = value; OnPropertyChanged(); }}
    } = "ARCHIVE // CONNECTING TO CKAN...";

    public DiscoverPageViewModel(IModApiService api)
    {
        _api = api;
        PageName = ApplicationPageNames.Discover;
        PopulateMods();
    }

    public DiscoverPageViewModel()
    {
    }

    private async Task PopulateMods()
    {
        var modList = await _api.GetModsAsync(1, 20);
        Mods = new ObservableCollection<Mod>(modList);
        ConnectionStatus = "ARCHIVE // ACQUIRED CKAN DATA FEED";
    }
    
    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task InstallMod(Mod mod)
    {
        var versionList = await _api.GetVersionsByModIdAsync(mod.Id);
        Console.WriteLine(JsonSerializer.Serialize(versionList, new JsonSerializerOptions
        {
            WriteIndented = true
        }));
    }
}