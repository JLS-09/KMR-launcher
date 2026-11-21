using System;
using System.Globalization;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using KMRLauncherMvvm.Models;
using KMRLauncherMvvm.Services;

namespace KMRLauncherMvvm.ViewModels.InstallMods;

public enum ProgressTone
{
    Muted,
    Done,
    Downloading,
    Installing,
    Failed
}

public partial class ModProgressRowViewModel(ModInstallJob job) : ObservableObject
{
    public ModInstallJob Job { get; } = job;
    public string Identifier => Job.Version.Identifier;
    public string Version => Job.Version.Version;

    [ObservableProperty] private string _stateText = "Queued";
    [ObservableProperty] private string _detailText = "";
    [ObservableProperty] private double _progress;
    [ObservableProperty] private bool _isIndeterminate;
    [ObservableProperty] private bool _isFailed;
    [ObservableProperty] private bool _isDone;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsToneDone), nameof(IsToneDownloading), nameof(IsToneInstalling),
        nameof(IsToneFailed), nameof(IsToneMuted))]
    private ProgressTone _tone = ProgressTone.Muted;

    public bool IsToneDone => Tone == ProgressTone.Done;
    public bool IsToneDownloading => Tone == ProgressTone.Downloading;
    public bool IsToneInstalling => Tone == ProgressTone.Installing;
    public bool IsToneFailed => Tone == ProgressTone.Failed;
    public bool IsToneMuted => Tone == ProgressTone.Muted;

    public double DownloadFraction { get; private set; }
    public double InstallFraction { get; private set; }

    public void Update()
    {
        var state = Job.State;
        var bytesDone = Interlocked.Read(ref Job.BytesDone);
        var bytesTotal = Job.BytesTotal;
        var filesDone = Job.FilesDone;
        var filesTotal = Job.FilesTotal;

        var downloaded = !Job.NeedsDownload || Job.IsCached ||
                         state is ModInstallState.Downloaded or ModInstallState.Installing or ModInstallState.Done;
        DownloadFraction = downloaded ? 1 : bytesTotal is > 0 ? Math.Min(1, (double)bytesDone / bytesTotal.Value) : 0;
        InstallFraction = state == ModInstallState.Done ? 1 : filesTotal > 0 ? (double)filesDone / filesTotal : 0;

        IsDone = state == ModInstallState.Done;
        IsFailed = state == ModInstallState.Failed;
        IsIndeterminate = state == ModInstallState.Downloading && bytesTotal is null or 0;

        (StateText, DetailText, Progress, Tone) = state switch
        {
            ModInstallState.Queued => ("Queued", Job.NeedsDownload ? "Waiting for a download slot" : "", 0,
                ProgressTone.Muted),
            ModInstallState.Downloading => ("Downloading",
                bytesTotal is > 0 ? BytePair(bytesDone, bytesTotal.Value) : InstalledMods.FormatSize(bytesDone),
                DownloadFraction, ProgressTone.Downloading),
            ModInstallState.Downloaded => ("Downloaded",
                Job.IsCached ? "Cached · waiting to install" : "Waiting to install", 1, ProgressTone.Downloading),
            ModInstallState.Installing => ("Installing", $"{Count(filesDone)} / {Count(filesTotal)} files",
                InstallFraction, ProgressTone.Installing),
            ModInstallState.Done => ("Done", DoneText(), 1, ProgressTone.Done),
            ModInstallState.Failed => ("Failed", FirstLine(Job.Error), Progress, ProgressTone.Failed),
            _ => ("Cancelled", "", Progress, ProgressTone.Muted)
        };
    }

    private string DoneText()
    {
        if (!Job.NeedsDownload) return "Nothing to download";

        var files = $"{Count(Job.FilesTotal)} files";
        if (Job.IsCached) return $"Cached · {files}";
        return Job.BytesTotal is > 0 ? $"{InstalledMods.FormatSize(Job.BytesTotal.Value)} · {files}" : files;
    }

    public static string Count(long count) => count.ToString("N0", CultureInfo.InvariantCulture);

    public static string BytePair(long done, long total)
    {
        if (total < 1024) return string.Create(CultureInfo.InvariantCulture, $"{done} / {total} B");

        var (divisor, unit) = total >= 1L << 30 ? (1024.0 * 1024 * 1024, "GB")
            : total >= 1L << 20 ? (1024.0 * 1024, "MB")
            : (1024.0, "KB");
        return string.Create(CultureInfo.InvariantCulture, $"{done / divisor:0.0} / {total / divisor:0.0} {unit}");
    }

    public static string FirstLine(string? text) =>
        text is null ? "" : text.Split('\n', 2)[0].Trim();
}
