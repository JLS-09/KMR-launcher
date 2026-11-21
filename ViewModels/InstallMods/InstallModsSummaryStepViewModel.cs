using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using KMRLauncherMvvm.Models;
using KMRLauncherMvvm.Services;

namespace KMRLauncherMvvm.ViewModels.InstallMods;

public partial class InstallModsSummaryStepViewModel(InstallModsData installModsData)
    : InstallModsStepViewModel(installModsData)
{
    [ObservableProperty] private IReadOnlyList<ChangeRow> _rows = [];
    [ObservableProperty] private IReadOnlyList<ResolutionProblem> _problems = [];
    [ObservableProperty] private string _newText = "0";
    [ObservableProperty] private string _updatesText = "0";
    [ObservableProperty] private string _removedText = "0";
    [ObservableProperty] private string _toDownloadText = "0 MB";
    [ObservableProperty] private string _okText = "";
    [ObservableProperty] private string _rowsTitle = "TO INSTALL";
    [ObservableProperty] private string _installText = "Install";
    [ObservableProperty] private bool _hasProblems;
    [ObservableProperty] private bool _hasNothingToDo;
    [ObservableProperty] private bool _canFinish;

    public Func<ModVersion, bool> IsCached { get; set; } = _ => false;

    public override string Title => "Review";
    public override bool CanGoNext => CanFinish;

    partial void OnCanFinishChanged(bool value) => OnPropertyChanged(nameof(CanGoNext));

    public override void Prepare() => Refresh();

    public override void OnEntering(bool forward) => Refresh();

    public void Refresh()
    {
        var instance = InstallModsData.SelectedInstance;
        Rows = InstallChanges.Rows(InstallModsData, IsCached);
        var counts = InstallChanges.Count(Rows);

        NewText = counts.New.ToString();
        UpdatesText = counts.Updates.ToString();
        RemovedText = counts.Removed.ToString();
        ToDownloadText = InstalledMods.FormatSize(counts.ToDownloadBytes);

        var installs = counts.New + counts.Updates;
        RowsTitle = installs > 0 ? "TO INSTALL" : "TO REMOVE";
        InstallText = installs > 0
            ? $"Install {InstallChanges.Plural(installs, "mod")}"
            : $"Remove {InstallChanges.Plural(counts.Removed, "mod")}";

        Problems = [.. InstallModsData.Problems];
        HasProblems = Problems.Count > 0;
        OkText = instance is null
            ? ""
            : $"No problems. No conflicts with the {InstallChanges.Plural(instance.Mods.Count, "installed mod")}, " +
              $"and everything fits KSP {instance.Version}.";

        HasNothingToDo = Rows.Count == 0;
        CanFinish = !HasProblems && !HasNothingToDo && instance is not null;
    }
}
