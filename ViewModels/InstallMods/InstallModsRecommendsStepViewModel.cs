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
        public bool SelectedByDefault { get; set; }
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
                    AddRelationship(recommendations, recommendation, source, criteria, true);

                foreach (var suggestion in planned.Version.Suggests ?? [])
                    AddRelationship(suggestions, suggestion, source, criteria, false);

                AddSupporting(supported, source, criteria);
            }
        }

        foreach (var identifier in recommendations.Keys)
        {
            suggestions.Remove(identifier);
            supported.Remove(identifier);
        }

        foreach (var identifier in suggestions.Keys)
            supported.Remove(identifier);

        Recommendations = BuildRows(recommendations, "Recommended by", previousRecommendations);
        Suggestions = BuildRows(suggestions, "Suggested by", previousSuggestions);
        Supported = BuildRows(supported, "Supports", previousSupported);

        ShowRecommendations = Recommendations.Count > 0;
        ShowSuggestions = Suggestions.Count > 0;
        ShowSupported = Supported.Count > 0;
    }

    private void AddRelationship(Dictionary<string, RowData> rows, Relationship relationship, string source,
        GameVersionCriteria? criteria, bool isRecommendation)
    {
        if (IsSatisfied(relationship)) return;

        var options = GetOptions(relationship, criteria);

        var selectedByDefault = isRecommendation && options.Count == 1;

        foreach (var (identifier, versions) in options)
            AddRow(rows, identifier, versions, source, selectedByDefault);
    }

    private List<(string Identifier, List<ModVersion> Versions)> GetOptions(Relationship relationship,
        GameVersionCriteria? criteria)
    {
        List<(string, List<ModVersion>)> options = [];

        List<(string? Name, string? Exact, string? Min, string? Max)> entries =
            relationship.AnyOf is { Count: > 0 } anyOf
                ? [.. anyOf.Select(e => (e.Name, e.Version, e.MinVersion, e.MaxVersion))]
                : [(relationship.Name, relationship.Version, relationship.MinVersion, relationship.MaxVersion)];

        foreach (var (name, exact, min, max) in entries)
        {
            if (name is null) continue;

            if (_compatibilityService.ModExists(name))
            {
                var versions = _compatibilityService.GetVersionsWithinLimits(name, exact, min, max, criteria);
                if (versions.Count > 0) options.Add((name, versions));
                continue;
            }

            foreach (var provider in _compatibilityService.GetVersionsFromModule(name, criteria))
                options.Add((provider.Identifier,
                    _compatibilityService.GetProvidingVersions(provider.Identifier, name, criteria)));
        }

        return [.. options.Where(o => !IsPlannedOrInstalled(o.Item1)).DistinctBy(o => o.Item1)];
    }

    private bool IsSatisfied(Relationship relationship)
    {
        HashSet<string> names = relationship.AnyOf is { Count: > 0 } anyOf
            ? [.. anyOf.Select(e => e.Name).OfType<string>()]
            : [.. new[] { relationship.Name }.OfType<string>()];

        return InstallModsData.FinalModList.Select(m => m.Version)
            .Concat(InstallModsData.SelectedInstance?.Mods ?? [])
            .Any(m => names.Contains(m.Identifier) || m.Provides?.Any(names.Contains) == true);
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

            AddRow(rows, mod.Id, versions, supportedMod, false);
        }
    }

    private static bool Supports(ModVersion version, string supportedMod) =>
        version.Supports?.Any(s =>
            string.Equals(s.Name, supportedMod, StringComparison.OrdinalIgnoreCase) ||
            s.AnyOf?.Any(a => string.Equals(a.Name, supportedMod, StringComparison.OrdinalIgnoreCase)) == true) == true;

    private static void AddRow(Dictionary<string, RowData> rows, string identifier, List<ModVersion> versions,
        string source, bool selectedByDefault)
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
        row.SelectedByDefault |= selectedByDefault;
    }

    private bool IsPlannedOrInstalled(string identifier) =>
        InstallModsData.FinalModList.Any(m => m.Version.Identifier == identifier) ||
        InstallModsData.SelectedInstance?.Mods.Any(m => m.Identifier == identifier) == true;

    private static List<VersionListItemViewModel> BuildRows(Dictionary<string, RowData> rows, string sourcesPrefix,
        Dictionary<string, RowState> previous) =>
        rows.Select(pair =>
        {
            var state = previous.GetValueOrDefault(pair.Key);
            return new VersionListItemViewModel(pair.Key, pair.Value.Versions, pair.Value.Sources, sourcesPrefix,
                state?.IsSelected ?? pair.Value.SelectedByDefault, state?.VersionId);
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

    public override void Prepare() => Rebuild();

    public override void OnEntering(bool forward)
    {
        if (!forward) Rebuild();
    }

    private void Rebuild()
    {
        _dependencyService.ResolveDependencies(InstallModsData);
        PopulateRecommendations();
        ShouldSkip = !ShowRecommendations && !ShowSuggestions && !ShowSupported;
    }

    public override void OnLeaving(bool forward)
    {
        if (forward) AddSelectedMods();
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