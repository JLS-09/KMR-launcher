using KMRLauncherMvvm.Data;
using KMRLauncherMvvm.Services.Api;

namespace KMRLauncherMvvm.ViewModels;

public partial class DiscoverPageViewModel : PageViewModel
{
    private readonly IModApiService _api;
    public string _mods = "";

    public string Mods
    {
        get => _mods;
        set { if (_mods != value) { _mods = value; OnPropertyChanged(); } }
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
        foreach (var mod in modList)
        {
            Mods = Mods + mod.Name + "\n";
        }
    }
}