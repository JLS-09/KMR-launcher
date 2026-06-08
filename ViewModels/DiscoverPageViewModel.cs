using System.Collections.ObjectModel;
using System.Threading.Tasks;
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
    private bool _isFetching;

    public ObservableCollection<Mod> ModList
    {
        get;
        set
        {
            if (field == value) return;
            field = value; OnPropertyChanged();
        }
    }

    private string? NextCursor
    {
        get;
        set
        {
            if (field == value) return;
            field = value; OnPropertyChanged();
        }
    }

    private bool HasNext
    {
        get;
        set
        {
            if (field == value) return;
            field = value; OnPropertyChanged();
        }
    }

    public string ModFilter
    {
        get;
        set
        {
            if (field == value) return;
            field = value; OnPropertyChanged();
        }
    }

    public string AuthorFilter
    {
        get;
        set
        {
            if (field == value) return;
            field = value; OnPropertyChanged();
        }
    }

    public string ConnectionStatus
    {
        get;
        set
        {
            if (field == value) return;
            field = value; OnPropertyChanged();
        }
    } = "ARCHIVE // CONNECTING TO CKAN...";

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
        var response = await _api.GetAllModsAsync();
        ModList = new ObservableCollection<Mod>(response);
        ConnectionStatus = "ARCHIVE // ACQUIRED CKAN DATA FEED";
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