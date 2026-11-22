using System;
using CommunityToolkit.Mvvm.ComponentModel;
using KMRLauncherMvvm.Models;
using KMRLauncherMvvm.Services;

namespace KMRLauncherMvvm.ViewModels.InstallMods;

public partial class ChangePanelViewModel : ObservableObject
{
    [ObservableProperty] private string _instanceName = "";
    [ObservableProperty] private string _kspText = "";
    [ObservableProperty] private string _newCount = "0";
    [ObservableProperty] private string _updatesCount = "0";
    [ObservableProperty] private string _removedCount = "0";
    [ObservableProperty] private bool _hasRemoved;
    [ObservableProperty] private string _downloadText = "0 MB";
    [ObservableProperty] private string _cachedText = "";
    [ObservableProperty] private bool _hasCached;
    [ObservableProperty] private string _problemsText = "No problems";
    [ObservableProperty] private bool _hasProblems;
    [ObservableProperty] private string _choicesLeftText = "";
    [ObservableProperty] private bool _hasChoicesLeft;

    public void Update(InstallModsData data, Func<ModVersion, bool> isCached, int choicesLeft)
    {
        var instance = data.SelectedInstance;
        InstanceName = instance?.Name ?? "No instance";
        KspText = instance is null ? "" : $"KSP {instance.CompatibleSummary}";

        var counts = InstallChanges.Count(InstallChanges.Rows(data, isCached));
        NewCount = counts.New.ToString();
        UpdatesCount = counts.Updates.ToString();
        RemovedCount = counts.Removed.ToString();
        HasRemoved = counts.Removed > 0;
        DownloadText = InstalledMods.FormatSize(counts.DownloadBytes);
        HasCached = counts.CachedBytes > 0;
        CachedText = InstalledMods.FormatSize(counts.CachedBytes);

        var problems = data.Problems.Count;
        HasProblems = problems > 0;
        ProblemsText = problems == 0 ? "No problems" : InstallChanges.Plural(problems, "problem");

        HasChoicesLeft = choicesLeft > 0;
        ChoicesLeftText = choicesLeft == 1 ? "1 choice left" : $"{choicesLeft} choices left";
    }
}
