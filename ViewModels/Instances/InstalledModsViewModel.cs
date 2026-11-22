using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KMRLauncherMvvm.Models;
using KMRLauncherMvvm.Services;
using KMRLauncherMvvm.ViewModels.InstallMods;

namespace KMRLauncherMvvm.ViewModels.Instances;

public partial class InstalledModsViewModel(ModListService modListService, InstallFlow installFlow) : ObservableObject
{
    private Instance? _instance;
    private List<InstalledModRowViewModel> _allRows = [];
    private Dictionary<string, string>? _sizes;
    private int _job;

    [ObservableProperty] private bool _isLoading;

    [ObservableProperty] private ObservableCollection<InstalledModRowViewModel> _rows = [];
    [ObservableProperty] private string _modFilterText = "";
    [ObservableProperty] private ModsFilter _filter = ModsFilter.All;
    [ObservableProperty] private string _allCount = "0";
    [ObservableProperty] private string _detail = "";
    [ObservableProperty] private string _pickedCount = "0";
    [ObservableProperty] private string _dependenciesCount = "0";
    [ObservableProperty] private string _updatesCount = "0";
    [ObservableProperty] private string _issuesCount = "0";
    [ObservableProperty] private bool _hasUpdates;
    [ObservableProperty] private string _shownText = "";

    [ObservableProperty] private int _selectedCount;
    [ObservableProperty] private bool _hasSelection;
    [ObservableProperty] private string _removalNote = "";
    [ObservableProperty] private bool _selectionHasUpdates;

    public async Task Show(Instance instance)
    {
        var job = ++_job;
        if (!ReferenceEquals(instance, _instance))
        {
            _instance = instance;
            _sizes = null;
            SetRows([]);
            AllCount = PickedCount = DependenciesCount = UpdatesCount = IssuesCount = "–";
            Detail = "";
            ShownText = "";
            IsLoading = true;
        }

        var modList = modListService.Mods;
        List<InstalledModRowViewModel> rows;
        try
        {
            rows = await Task.Run(() => BuildRows(instance, modList));
        }
        catch (Exception) when (job != _job)
        {
            return;
        }

        if (job != _job) return;
        SetRows(rows);
        IsLoading = false;
    }

    private static List<InstalledModRowViewModel> BuildRows(Instance instance, IEnumerable<Mod>? modList)
    {
        UnmanagedDlls.Refresh(instance);
        var criteria = GameVersionCriteria.FromInstance(instance);
        var mods = modList?.ToDictionary(m => m.Id) ?? [];

        return
        [
            .. instance.Mods
                .Select(v => new InstalledModRowViewModel(instance, v, mods.GetValueOrDefault(v.Identifier), criteria,
                    InstalledMods.FindReplacement(instance, v, mods, criteria)))
                .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
        ];
    }

    private void SetRows(List<InstalledModRowViewModel> rows)
    {
        foreach (var row in _allRows) row.SelectionChanged -= OnRowSelectionChanged;
        _allRows = rows;
        foreach (var row in _allRows)
        {
            row.SizeText = _sizes is null ? "" : _sizes.GetValueOrDefault(row.Version.Identifier, "–");
            row.SelectionChanged += OnRowSelectionChanged;
        }

        var picked = _allRows.Count(r => !r.IsDependency);
        var updates = _allRows.Count(r => r.HasUpdateOrReplacement);
        AllCount = _allRows.Count.ToString();
        Detail = $"{picked} picked · {_allRows.Count - picked} dependencies";
        PickedCount = picked.ToString();
        DependenciesCount = (_allRows.Count - picked).ToString();
        UpdatesCount = updates.ToString();
        IssuesCount = _allRows.Count(r => r.IsNotListed).ToString();
        HasUpdates = updates > 0;

        ApplyModFilter();
        OnRowSelectionChanged();
    }

    public void ShowSizes(IReadOnlyDictionary<string, long> modSizes)
    {
        _sizes = modSizes.Where(p => p.Value > 0).ToDictionary(p => p.Key, p => InstalledMods.FormatSize(p.Value));
        foreach (var row in _allRows) row.SizeText = _sizes.GetValueOrDefault(row.Version.Identifier, "–");
    }

    partial void OnModFilterTextChanged(string value) => ApplyModFilter();
    partial void OnFilterChanged(ModsFilter value) => ApplyModFilter();

    [RelayCommand]
    private void SetFilter(ModsFilter filter) => Filter = filter;

    private void ApplyModFilter()
    {
        var text = ModFilterText.Trim();
        Rows = new ObservableCollection<InstalledModRowViewModel>(_allRows.Where(r =>
            (text.Length == 0 || r.Name.Contains(text, StringComparison.OrdinalIgnoreCase) ||
             r.Version.Identifier.Contains(text, StringComparison.OrdinalIgnoreCase)) &&
            Filter switch
            {
                ModsFilter.Picked => !r.IsDependency,
                ModsFilter.Dependencies => r.IsDependency,
                ModsFilter.Updates => r.HasUpdateOrReplacement,
                ModsFilter.Issues => r.IsNotListed,
                _ => true
            }));
        ShownText = $"Showing {Rows.Count} of {_allRows.Count}";
    }

    private void OnRowSelectionChanged()
    {
        var selected = _allRows.Where(r => r.IsSelected).ToList();
        SelectedCount = selected.Count;
        HasSelection = selected.Count > 0;
        SelectionHasUpdates = selected.Any(r => r.HasUpdateOrReplacement);
        RemovalNote = _instance is null || selected.Count == 0
            ? ""
            : InstalledMods.DescribeRemoval(InstalledMods.PlanRemoval(_instance,
                [.. selected.Select(r => r.Version.Identifier)]));
    }

    [RelayCommand]
    private void ClearSelection()
    {
        foreach (var row in _allRows) row.IsSelected = false;
    }

    [RelayCommand]
    private Task RemoveSelected() => Remove([.. _allRows.Where(r => r.IsSelected)]);

    [RelayCommand]
    private Task RemoveRow(InstalledModRowViewModel row) => Remove([row]);

    private async Task Remove(IReadOnlyList<InstalledModRowViewModel> rows)
    {
        if (_instance is not { } instance || rows.Count == 0) return;

        var plan = InstalledMods.PlanRemoval(instance, [.. rows.Select(r => r.Version.Identifier)]);
        await installFlow.RemoveAsync(instance, plan.All);
    }

    [RelayCommand]
    private Task UpdateSelected() => Update([.. _allRows.Where(r => r.IsSelected && r.HasUpdateOrReplacement)]);

    [RelayCommand]
    private Task UpdateAll() => Update([.. _allRows.Where(r => r.HasUpdateOrReplacement)]);

    [RelayCommand]
    private Task UpdateRow(InstalledModRowViewModel row) => Update([row]);

    private async Task Update(IReadOnlyList<InstalledModRowViewModel> rows)
    {
        if (_instance is not { } instance || rows.Count == 0) return;

        var requested = rows.Select(r => new PlannedModVersion
        {
            Version = r.Replacement ?? r.NextVersion!,
            IsUserRequested = InstalledMods.IsUserRequested(instance, r.Version.Identifier),
            IsVersionPickedByUser = true,
            OptionalReason = instance.ModRecords.GetValueOrDefault(r.Version.Identifier)?.Reason,
            Replaces = r.HasReplacement ? r.Version.Identifier : null
        }).ToList();
        List<ModVersion> replaced = [.. rows.Where(r => r.HasReplacement).Select(r => r.Version)];

        if (await installFlow.InstallAsync(requested, instance, InstallSource.Updates, replaced)) ClearSelection();
    }
}
