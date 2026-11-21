using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using KMRLauncherMvvm.Models;
using KMRLauncherMvvm.Services;

namespace KMRLauncherMvvm.ViewModels.InstallMods;

public partial class InstallResultViewModel(Func<ModVersion, bool> isInCache) : ObservableObject
{
    private const int FailedRowsShown = 5;

    [ObservableProperty] private string _doneTitle = "";
    [ObservableProperty] private string _doneSummary = "";
    [ObservableProperty] private IReadOnlyList<StatTile> _doneTiles = [];
    [ObservableProperty] private string _launchText = "";
    [ObservableProperty] private string _selectionNote = "";
    [ObservableProperty] private string _failedTitle = "";
    [ObservableProperty] private string _failedSummary = "";
    [ObservableProperty] private string _errorText = "";
    [ObservableProperty] private bool _showError;
    [ObservableProperty] private IReadOnlyList<FailedRow> _failedRows = [];
    [ObservableProperty] private string _failedRowsCount = "";
    [ObservableProperty] private string _moreFailedText = "";
    [ObservableProperty] private bool _hasMoreFailed;
    [ObservableProperty] private string _cacheHint = "";

    public void ShowDone(Instance instance, InstallProgressViewModel progress, ChangeCounts counts,
        string selectionNote)
    {
        var jobs = progress.Run.Jobs;
        var installs = counts.New + counts.Updates;
        var took = $"took {InstallChanges.Duration(progress.Elapsed)}";

        if (installs > 0)
        {
            DoneTitle = $"Installed into {instance.Name}";
            List<string> parts = [];
            if (counts.New > 0) parts.Add($"{counts.New} new");
            if (counts.Updates > 0) parts.Add($"{counts.Updates} updated");
            if (counts.Removed > 0) parts.Add($"{counts.Removed} removed");
            DoneSummary = $"{InstallChanges.Plural(installs, "mod")} · {string.Join(", ", parts)} · {took}";

            var downloaded = jobs.Where(j => j.NeedsDownload && !j.IsCached).Sum(j => j.BytesTotal ?? 0);
            DoneTiles =
            [
                new StatTile("DOWNLOADED", InstalledMods.FormatSize(downloaded)),
                new StatTile("FILES", ModProgressRowViewModel.Count(jobs.Sum(j => (long)j.FilesTotal))),
                new StatTile("MODS NOW", instance.Mods.Count.ToString())
            ];
        }
        else
        {
            DoneTitle = $"Removed from {instance.Name}";
            DoneSummary = $"{InstallChanges.Plural(counts.Removed, "mod")} removed · {took}";
            DoneTiles =
            [
                new StatTile("REMOVED", counts.Removed.ToString()),
                new StatTile("MODS NOW", instance.Mods.Count.ToString())
            ];
        }

        LaunchText = $"Launch {instance.Name}";
        SelectionNote = selectionNote;
    }

    public void ShowFailed(Instance instance, InstallProgressViewModel progress, IReadOnlySet<string> installedBefore)
    {
        var jobs = progress.Run.Jobs;
        var removing = jobs.Count == 0;
        var notUndone = progress.ErrorMessage.Contains("couldn't be undone");

        FailedTitle = (removing ? "Removing" : "Install") + (progress.WasCancelled ? " cancelled" : " failed");

        var updated = jobs.Where(j => j.State == ModInstallState.Done)
            .Select(j => j.Version.Identifier).Where(installedBefore.Contains).ToList();
        FailedSummary = notUndone
            ? $"Some changes in {instance.Name} couldn't be undone; see below."
            : $"Nothing was changed in {instance.Name}." + updated.Count switch
            {
                0 => "",
                1 => $" {updated[0]} is back on its old version.",
                _ => $" {InstallChanges.JoinAll(updated)} are back on their old versions."
            };

        ErrorText = progress.ErrorMessage;
        ShowError = !progress.WasCancelled || notUndone;

        var rows = jobs.Select(FailedRowFor).OrderBy(r => r.Order).ToList();
        FailedRows = [.. rows.Take(FailedRowsShown).Select(r => r.Row)];
        FailedRowsCount = rows.Count.ToString();
        var more = rows.Skip(FailedRowsShown).ToList();
        HasMoreFailed = more.Count > 0;
        MoreFailedText = more.Count == 0 ? ""
            : $"and {more.Count} more" + (more.All(r => r.Cached) ? ", all kept in the cache" : "");

        CacheHint = CacheHintFor(jobs);
    }

    private (int Order, FailedRow Row, bool Cached) FailedRowFor(ModInstallJob job)
    {
        var cached = job.NeedsDownload && isInCache(job.Version);
        var keptText = cached ? "Kept in cache" : job.NeedsDownload ? "" : "Nothing to download";
        var downloaded = job.BytesTotal is > 0 ? Math.Min(1, (double)job.BytesDone / job.BytesTotal.Value) : 0;

        return job.State switch
        {
            ModInstallState.Failed => (0,
                new FailedRow(job.Version.Identifier, "Failed", Math.Max(downloaded, 0.05),
                    ShortError(job.Error), true), cached),
            ModInstallState.Done => (1, new FailedRow(job.Version.Identifier, "Rolled back", 1, keptText, false),
                cached),
            _ when cached => (2, new FailedRow(job.Version.Identifier, "Cancelled", 1, keptText, false), true),
            _ when job.BytesDone > 0 => (2,
                new FailedRow(job.Version.Identifier, "Cancelled", downloaded, "Partial download deleted", false),
                false),
            _ => (3, new FailedRow(job.Version.Identifier, "Not started", 0, "", false), !job.NeedsDownload)
        };
    }

    public static string ShortError(string? error)
    {
        if (error is null) return "";

        var lines = error.Split('\n');
        if (!lines[0].EndsWith("could not be downloaded.") || lines.Length < 2)
            return ModProgressRowViewModel.FirstLine(error);

        return lines.Length > 2
            ? "All mirrors failed"
            : string.Join(" ", lines[1].Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries).Skip(1)).Trim();
    }

    private string CacheHintFor(IReadOnlyList<ModInstallJob> jobs)
    {
        var downloads = jobs.Where(j => j.NeedsDownload).ToList();
        if (downloads.Count == 0) return "";

        var missing = downloads.Where(j => !isInCache(j.Version)).ToList();
        if (missing.Count == 0) return "All downloads are cached, so a retry doesn't download anything.";

        var names = missing.Select(j => j.Version.Identifier).ToList();
        var size = missing.Sum(j => j.Version.DownloadSize ?? 0);
        var list = names.Count <= 3
            ? InstallChanges.JoinAll(names)
            : $"{string.Join(", ", names.Take(2))} and {names.Count - 2} more";
        var sizeText = size > 0 ? $" ({InstalledMods.FormatSize(size)})" : "";

        return missing.Count == downloads.Count
            ? $"Nothing was downloaded yet; a retry downloads {list}{sizeText}."
            : $"{downloads.Count - missing.Count} of {downloads.Count} downloads are cached, so a retry only " +
              $"downloads {list}{sizeText}.";
    }
}
