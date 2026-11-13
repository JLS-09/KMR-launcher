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
    private readonly ModListService _modListService;
    private readonly CompatibilityService _compatibilityService;
    private readonly DependencyService _dependencyService;
    [ObservableProperty] private List<VersionListItemViewModel> _recommendations = [];
    [ObservableProperty] private List<VersionListItemViewModel> _suggestions = [];
    [ObservableProperty] private List<VersionListItemViewModel> _supported = [];
    [ObservableProperty] private bool _showRecommendations;
    [ObservableProperty] private bool _showSuggestions;
    [ObservableProperty] private bool _showSupported;

    public InstallModsRecommendsStepViewModel(InstallModsData installModsData, ModListService modListService,
        CompatibilityService compatibilityService, DependencyService dependencyService) : base(installModsData)
    {
        _modListService = modListService;
        _compatibilityService = compatibilityService;
        _dependencyService = dependencyService;
    }

    private sealed class RowData(List<ModVersion> versions)
    {
        public List<ModVersion> Versions { get; set; } = versions;
        public List<string> Sources { get; } = [];
    }

    private sealed record RowState(bool IsSelected, string? VersionId);

    private void PopulateRecommendations()
    {
        var previousRecommendations = RowStates(Recommendations);
        var previousSuggestions = RowStates(Suggestions);
        var previousSupported = RowStates(Supported);

        Dictionary<string, RowData> recommendations = [];
        Dictionary<string, RowData> suggestions = [];
        Dictionary<string, RowData> supported = [];

        if (_modListService.Mods is not null)
        {
            var criteria = InstallModsData.SelectedInstance is { } instance
                ? GameVersionCriteria.FromInstance(instance)
                : null;

            foreach (var planned in InstallModsData.FinalModList)
            {
                if (InstallModsData.ModsWithIgnoredRecommendations.Contains(planned.Version.Identifier))
                    continue;

                var source = planned.Version.Identifier;

                foreach (var recommendation in planned.Version.Recommends ?? [])
                    AddRelationship(recommendations, recommendation, source, criteria);

                foreach (var suggestion in planned.Version.Suggests ?? [])
                    AddRelationship(suggestions, suggestion, source, criteria);

                AddSupporting(supported, source, criteria);
            }
        }

        Recommendations = BuildRows(recommendations, "Recommended by", previousRecommendations, true);
        Suggestions = BuildRows(suggestions, "Suggested by", previousSuggestions, false);
        Supported = BuildRows(supported, "Supports", previousSupported, false);

        ShowRecommendations = Recommendations.Count > 0;
        ShowSuggestions = Suggestions.Count > 0;
        ShowSupported = Supported.Count > 0;
    }

    private void AddRelationship(Dictionary<string, RowData> rows, Relationship relationship, string source,
        GameVersionCriteria? criteria)
    {
        if (relationship.Name is not { } name || IsPlannedOrInstalled(name)) return;

        var versions = _compatibilityService.GetVersionsWithinLimits(name, relationship.Version,
            relationship.MinVersion, relationship.MaxVersion, criteria);
        if (versions.Count == 0) return;

        AddRow(rows, name, versions, source);
    }

    private void AddSupporting(Dictionary<string, RowData> rows, string supportedMod, GameVersionCriteria? criteria)
    {
        foreach (var mod in _modListService.Mods!)
        {
            if (IsPlannedOrInstalled(mod.Id)) continue;

            var versions = mod.Versions
                .Where(v => Supports(v, supportedMod) && CompatibilityService.IsCompatible(v, criteria))
                .ToList();
            if (versions.Count == 0) continue;

            AddRow(rows, mod.Id, versions, supportedMod);
        }
    }

    private static bool Supports(ModVersion version, string supportedMod) =>
        version.Supports?.Any(s =>
            string.Equals(s.Name, supportedMod, StringComparison.OrdinalIgnoreCase) ||
            s.AnyOf?.Any(a => string.Equals(a.Name, supportedMod, StringComparison.OrdinalIgnoreCase)) == true) == true;

    private static void AddRow(Dictionary<string, RowData> rows, string identifier, List<ModVersion> versions,
        string source)
    {
        if (rows.TryGetValue(identifier, out var row))
        {
            var shared = row.Versions.Where(versions.Contains).ToList();
            if (shared.Count > 0) row.Versions = shared;
        }
        else
        {
            row = new RowData(versions);
            rows[identifier] = row;
        }

        if (!row.Sources.Contains(source)) row.Sources.Add(source);
    }

    private bool IsPlannedOrInstalled(string identifier) =>
        InstallModsData.FinalModList.Any(m => m.Version.Identifier == identifier) ||
        InstallModsData.SelectedInstance?.Mods.Any(m => m.Identifier == identifier) == true;

    private static List<VersionListItemViewModel> BuildRows(Dictionary<string, RowData> rows, string sourcesPrefix,
        Dictionary<string, RowState> previous, bool selectedByDefault) =>
        rows.Select(pair =>
        {
            var state = previous.GetValueOrDefault(pair.Key);
            return new VersionListItemViewModel(pair.Key, pair.Value.Versions, pair.Value.Sources, sourcesPrefix,
                state?.IsSelected ?? selectedByDefault, state?.VersionId);
        }).ToList();

    private static Dictionary<string, RowState> RowStates(List<VersionListItemViewModel> items) =>
        items.ToDictionary(i => i.Identifier, i => new RowState(i.IsSelected, i.SelectedVersion?.Id));

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
            .. Selected(Recommendations),
            .. Selected(Suggestions),
            .. Selected(Supported)
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

    private static IEnumerable<PlannedModVersion> Selected(List<VersionListItemViewModel> items) =>
        items.Where(i => i.IsSelected && i.SelectedVersion is not null).Select(i => new PlannedModVersion
        {
            Version = i.SelectedVersion!,
            IsUserRequested = true,
            OptionalReason = i.SourcesText
        });

    public override string Title => "Choose Recommendations";
    public override bool CanGoNext => true;
}