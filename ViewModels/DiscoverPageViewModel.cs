using System.Collections.ObjectModel;
using KMRLauncherMvvm.Data;
using KMRLauncherMvvm.Models;
using KMRLauncherMvvm.Services.Api;

namespace KMRLauncherMvvm.ViewModels;

public partial class DiscoverPageViewModel : PageViewModel
{
    private readonly IModApiService _api;
    private ObservableCollection<Mod> _mods;
    private string _connectionStatus = "ARCHIVE // CONNECTING TO CKAN...";

    public ObservableCollection<Mod> Mods
    {
        get => _mods;
        set { if (_mods != value) { _mods = value; OnPropertyChanged(); } }
    }
    
    public string ConnectionStatus
    {
        get => _connectionStatus;
        set { if (_connectionStatus != value) { _connectionStatus = value; OnPropertyChanged(); } }
    }

    public DiscoverPageViewModel(IModApiService api)
    {
        _api = api;
        PageName = ApplicationPageNames.Discover;
        PopulateMods();
    }

    public DiscoverPageViewModel()
    {
    }

    private async void PopulateMods()
    {
        var modList = await _api.GetModsAsync(1, 20);
        Mods = new ObservableCollection<Mod>(modList);
        ConnectionStatus = "ARCHIVE // ACQUIRED CKAN DATA FEED";
    }
}