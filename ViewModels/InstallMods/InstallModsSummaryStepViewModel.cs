using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Humanizer;
using KMRLauncherMvvm.Models;

namespace KMRLauncherMvvm.ViewModels.InstallMods;

public sealed record SummaryRow(string Identifier, string Version, string Details, string Size);

public partial class InstallModsSummaryStepViewModel(InstallModsData installModsData)
    : InstallModsStepViewModel(installModsData)
{
    [ObservableProperty] private List<SummaryRow> _toInstall = [];
    [ObservableProperty] private List<SummaryRow> _toRemove = [];
    [ObservableProperty] private string _totalsText = "";
    [ObservableProperty] private bool _hasRemovals;
    [ObservableProperty] private bool _hasProblems;
    [ObservableProperty] private bool _hasNothingToDo;
    [ObservableProperty] private bool _canFinish;

    public override string Title => "Summary";
    public override bool CanGoNext => CanFinish;

    partial void OnCanFinishChanged(bool value) => OnPropertyChanged(nameof(CanGoNext));

    public override void Prepare() => Refresh();

    public override void OnEntering(bool forward) => Refresh();

    private void Refresh()
    {
        var install = InstallModsData.FinalModList.ToList();
        var installed = InstallModsData.SelectedInstance?.Mods ?? [];
        var installIdentifiers = install.Select(m => m.Version.Identifier).ToHashSet();

        ToInstall =
        [
            .. install
                .OrderBy(m => m.Version.Identifier, System.StringComparer.OrdinalIgnoreCase)
                .Select(m => new SummaryRow(m.Version.Identifier, m.Version.Version,
                    InstallDetails(m, installed), FormatSize(m.Version.DownloadSize)))
        ];

        ToRemove =
        [
            .. InstallModsData.RequestedRemoveModVersions
                .Where(m => !installIdentifiers.Contains(m.Version.Identifier))
                .Select(m => new SummaryRow(m.Version.Identifier, m.Version.Version, "Removed from the instance", ""))
        ];

        var sizes = install.Select(m => m.Version.DownloadSize).ToList();
        var total = sizes.OfType<int>().Sum(s => (long)s);
        var unknown = sizes.Count(s => s is null);

        TotalsText = $"{Plural(install.Count, "mod")} to install, {FormatSize(total)} to download" +
                     (unknown > 0 ? $" (size unknown for {Plural(unknown, "mod")})" : "") +
                     (ToRemove.Count > 0 ? $", {Plural(ToRemove.Count, "mod")} to remove" : "");

        HasRemovals = ToRemove.Count > 0;
        HasProblems = InstallModsData.Problems.Count > 0;
        HasNothingToDo = install.Count == 0 && ToRemove.Count == 0;
        CanFinish = !HasProblems && !HasNothingToDo && InstallModsData.SelectedInstance is not null;
    }

    private static string InstallDetails(PlannedModVersion mod, List<ModVersion> installed)
    {
        var replaced = installed.FirstOrDefault(i => i.Identifier == mod.Version.Identifier);
        return replaced is null
            ? mod.ReasonForAction
            : $"{mod.ReasonForAction} · replaces {replaced.Version}";
    }

    private static string FormatSize(long? bytes) => bytes is { } b ? b.Bytes().Humanize("0.#") : "";

    private static string Plural(int count, string word) => count == 1 ? $"1 {word}" : $"{count} {word}s";
}