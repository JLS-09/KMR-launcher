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
        var response = await _api.GetModsAsync(10, null, ModFilter, AuthorFilter);
        ModList = new ObservableCollection<Mod>(response.Data);
        NextCursor = response.Pagination.NextCursor;
        HasNext = response.Pagination.HasNextPage;
        ConnectionStatus = "ARCHIVE // ACQUIRED CKAN DATA FEED";
    }

    public async Task FetchMoreMods()
    {
        if (_isFetching || !HasNext) return;

        try
        {
            _isFetching = true;
            ConnectionStatus = "ARCHIVE // FETCHING FROM CKAN...";
            var response = await _api.GetModsAsync(10, NextCursor, ModFilter, AuthorFilter);
            foreach (var mod in response.Data)
            {
                ModList.Add(mod);
            }
            NextCursor = response.Pagination.NextCursor;
            HasNext = response.Pagination.HasNextPage;
        }
        finally
        {
            _isFetching = false;
            ConnectionStatus = "ARCHIVE // ACQUIRED CKAN DATA FEED";
        }
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