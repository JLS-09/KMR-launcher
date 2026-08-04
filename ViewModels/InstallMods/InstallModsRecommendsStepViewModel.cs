using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using KMRLauncherMvvm.Models;

namespace KMRLauncherMvvm.ViewModels.InstallMods;

public partial class InstallModsRecommendsStepViewModel : InstallModsStepViewModel
{
    private ModListService _modListService;
    [ObservableProperty] private List<ModVersion> _recommendations = [];
    [ObservableProperty] private List<ModVersion> _suggestions = [];
    [ObservableProperty] private List<ModVersion> _supported = [];
    [ObservableProperty] private bool _showRecommendations;
    [ObservableProperty] private bool _showSuggestions;
    [ObservableProperty] private bool _showSupported;

    public InstallModsRecommendsStepViewModel(InstallModsData installModsData, ModListService modListService) : base(
        installModsData)
    {
        _modListService = modListService;
    }

    public override void PopulateRecommendations()
    {
        Recommendations = [];
        Suggestions = [];
        Supported = [];
        
        if (_modListService.Mods is null || !InstallModsData.RequestedModVersions.Any()) return;
        
        foreach (var version in InstallModsData.RequestedModVersions)
        {
            if (version.Recommends is not null && version.Recommends.Count > 0)
            {
                foreach (var recommendation in version.Recommends)
                {
                    if (_modListService.Mods.FirstOrDefault(m => m.Id.Equals(recommendation.Name)) is null)
                        continue;
                    
                    Recommendations.Add(_modListService.Mods.First(m => m.Id.Equals(recommendation.Name)).Versions
                        .First());
                }
            }
            
            if (version.Suggests is not null && version.Suggests.Count > 0)
            {
                foreach (var suggestion in version.Suggests)
                {
                    if (_modListService.Mods.FirstOrDefault(m => m.Id.Equals(suggestion.Name)) is null)
                        continue;
                    
                    Suggestions.Add(_modListService.Mods.First(m => m.Id.Equals(suggestion.Name)).Versions
                        .First());
                }
            }
        }

        ShowRecommendations = Recommendations.Count > 0;
        ShowSuggestions = Suggestions.Count > 0;
        ShowSupported = Supported.Count > 0;
    }

    public override string Title => "Choose Recommendations";
    public override bool CanGoNext => true;
}