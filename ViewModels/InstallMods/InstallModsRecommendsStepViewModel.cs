using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KMRLauncherMvvm.Models;
using KMRLauncherMvvm.Services;

namespace KMRLauncherMvvm.ViewModels.InstallMods;

public partial class InstallModsRecommendsStepViewModel : InstallModsStepViewModel
{
    private ModListService _modListService;
    private readonly DependencyService _dependencyService;
    [ObservableProperty] private List<VersionListItemViewModel> _recommendations = [];
    [ObservableProperty] private List<VersionListItemViewModel> _suggestions = [];
    [ObservableProperty] private List<VersionListItemViewModel> _supported = [];
    [ObservableProperty] private bool _showRecommendations;
    [ObservableProperty] private bool _showSuggestions;
    [ObservableProperty] private bool _showSupported;

    public InstallModsRecommendsStepViewModel(InstallModsData installModsData, ModListService modListService,
        DependencyService dependencyService) : base(installModsData)
    {
        _modListService = modListService;
        _dependencyService = dependencyService;
    }

    private void PopulateRecommendations()
    {
        var previousRecommendations = SelectionState(Recommendations);
        var previousSuggestions = SelectionState(Suggestions);
        var previousSupported = SelectionState(Supported);

        Recommendations = [];
        Suggestions = [];
        Supported = [];

        if (_modListService.Mods is null || !InstallModsData.FinalModList.Any()) return;

        var criteria = InstallModsData.SelectedInstance is { } instance
            ? GameVersionCriteria.FromInstance(instance)
            : null;

        foreach (var version in InstallModsData.FinalModList)
        {
            if (InstallModsData.ModsWithIgnoredRecommendations.Contains(version.Version.Identifier))
                continue;

            var source = version.Version.Identifier;

            if (version.Version.Recommends is not null && version.Version.Recommends.Count > 0)
            {
                foreach (var recommendation in version.Version.Recommends)
                {
                    if (InstallModsData.FinalModList.Any(v => v.Version.Identifier.Equals(recommendation.Name)) ||
                        (InstallModsData.SelectedInstance is not null &&
                         InstallModsData.SelectedInstance.Mods.Any(v => v.Identifier.Equals(recommendation.Name))) ||
                        _modListService.Mods.FirstOrDefault(m => m.Id.Equals(recommendation.Name)) is null)
                        continue;

                    var recommended = FirstCompatibleVersion(recommendation.Name, criteria);
                    if (recommended is not null)
                        AddOrMerge(Recommendations, recommended, source, previousRecommendations, true);
                }
            }

            if (version.Version.Suggests is not null && version.Version.Suggests.Count > 0)
            {
                foreach (var suggestion in version.Version.Suggests)
                {
                    if (InstallModsData.FinalModList.Any(v => v.Version.Identifier.Equals(suggestion.Name)) ||
                        (InstallModsData.SelectedInstance is not null &&
                         InstallModsData.SelectedInstance.Mods.Any(v => v.Identifier.Equals(suggestion.Name))) ||
                        _modListService.Mods.FirstOrDefault(m => m.Id.Equals(suggestion.Name)) is null)
                        continue;

                    var suggested = FirstCompatibleVersion(suggestion.Name, criteria);
                    if (suggested is not null)
                        AddOrMerge(Suggestions, suggested, source, previousSuggestions, false);
                }
            }

            var supporting = _modListService.Mods
                .SelectMany(mod => mod.Versions)
                .Where(v => v.Supports?.Any(s =>
                    string.Equals(s.Name, version.Version.Identifier, StringComparison.OrdinalIgnoreCase) ||
                    (s.AnyOf?.Any(a =>
                         string.Equals(a.Name, version.Version.Identifier, StringComparison.OrdinalIgnoreCase)) ??
                     false)
                ) ?? false)
                .Where(v => CompatibilityService.IsCompatible(v, criteria));

            foreach (var supportingVersion in supporting)
                Supported.Add(new VersionListItemViewModel(supportingVersion, source,
                    previousSupported.GetValueOrDefault(supportingVersion.Identifier, false)));
        }

        ShowRecommendations = Recommendations.Count > 0;
        ShowSuggestions = Suggestions.Count > 0;
        ShowSupported = Supported.Count > 0;
    }

    private static Dictionary<string, bool> SelectionState(List<VersionListItemViewModel> items) =>
        items.GroupBy(i => i.Version.Identifier).ToDictionary(g => g.Key, g => g.First().IsSelected);

    private static void AddOrMerge(List<VersionListItemViewModel> items, ModVersion version, string source,
        Dictionary<string, bool> previousSelection, bool selectedByDefault)
    {
        var existing = items.FirstOrDefault(i => i.Version.Identifier == version.Identifier);
        if (existing is not null)
        {
            existing.Sources.Add(source);
            return;
        }

        items.Add(new VersionListItemViewModel(version, source,
            previousSelection.GetValueOrDefault(version.Identifier, selectedByDefault)));
    }

    private ModVersion? FirstCompatibleVersion(string? identifier, GameVersionCriteria? criteria) =>
        _modListService.Mods?
            .FirstOrDefault(m => m.Id == identifier)?
            .Versions.FirstOrDefault(v => CompatibilityService.IsCompatible(v, criteria));

    [RelayCommand]
    private void ToggleRecommendation(VersionListItemViewModel item)
    {
        item.IsSelected = !item.IsSelected;
    }

    [RelayCommand]
    private void SelectAll(List<VersionListItemViewModel> versions)
    {
        foreach (var version in versions)
            version.IsSelected = true;
    }

    [RelayCommand]
    private void UnselectAll(List<VersionListItemViewModel> versions)
    {
        foreach (var version in versions)
            version.IsSelected = false;
    }

    public override void OnEntering(InstallModsStepViewModel? previous)
    {
        _dependencyService.ResolveDependencies(InstallModsData);
        PopulateRecommendations();
        base.OnEntering(previous);
    }

    public override void OnLeaving(InstallModsStepViewModel? next)
    {
        base.OnLeaving(next);

        if (next is InstallModsSelectInstanceStepViewModel) return;

        AddSelectedMods();
    }
    
    private void AddSelectedMods()
    {
        List<PlannedModVersion> additions =
        [
            .. Selected(Recommendations, "Recommended by"),
            .. Selected(Suggestions, "Suggested by"),
            .. Selected(Supported, "Supports")
        ];
        additions = [.. additions.DistinctBy(a => a.Version.Identifier)];
        if (additions.Count == 0) return;

        var result = _dependencyService.ResolveAdditional(InstallModsData.SelectedInstance,
            InstallModsData.FinalModList, additions);

        var removes = InstallModsData.RequestedRemoveModVersions.ToList();
        removes.AddRange(result.ToRemove.Where(r => !removes.Exists(w => w.Version.Id == r.Version.Id)));

        InstallModsData.FinalModList = new ObservableCollection<PlannedModVersion>(result.FinalModList);
        InstallModsData.RequestedRemoveModVersions = new ObservableCollection<PlannedModVersion>(removes);
        InstallModsData.ChoosableVersions = [.. InstallModsData.ChoosableVersions, .. result.NewChoosables];
        InstallModsData.Problems = [.. InstallModsData.Problems, .. result.Problems];
        InstallModsData.ModsWithIgnoredRecommendations =
            [.. DependencyService.FindSuppressedRecommendations(result.FinalModList)];
    }

    private static IEnumerable<PlannedModVersion> Selected(List<VersionListItemViewModel> items, string reason) =>
        items.Where(i => i.IsSelected).Select(i => new PlannedModVersion
        {
            Version = i.Version,
            IsUserRequested = true,
            OptionalReason = $"{reason} {string.Join(", ", i.Sources)}"
        });

    public override string Title => "Choose Recommendations";
    public override bool CanGoNext => true;
}