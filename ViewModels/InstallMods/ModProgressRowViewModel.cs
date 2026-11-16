using System;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Humanizer;
using KMRLauncherMvvm.Models;

namespace KMRLauncherMvvm.ViewModels.InstallMods;

public partial class ModProgressRowViewModel(ModInstallJob job) : ObservableObject
{
    public ModInstallJob Job { get; } = job;
    public string Identifier => Job.Version.Identifier;
    public string Version => Job.Version.Version;

    [ObservableProperty] private string _stateText = "QUEUED";
    [ObservableProperty] private string _detailText = "";
    [ObservableProperty] private double _progress;
    [ObservableProperty] private bool _isIndeterminate;
    [ObservableProperty] private bool _isFailed;
    [ObservableProperty] private bool _isDone;

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

        (StateText, DetailText, Progress) = state switch
        {
            ModInstallState.Queued => ("QUEUED", Job.NeedsDownload ? "Waiting for a download slot" : "", 0),
            ModInstallState.Downloading => ("DOWNLOADING",
                bytesTotal is > 0
                    ? $"{bytesDone.Bytes().Humanize("0.#")} / {bytesTotal.Value.Bytes().Humanize("0.#")}"
                    : bytesDone.Bytes().Humanize("0.#"),
                DownloadFraction),
            ModInstallState.Downloaded => ("DOWNLOADED",
                Job.IsCached ? "From the download cache, waiting to install" : "Waiting to install", 1),
            ModInstallState.Installing => ("INSTALLING", $"{filesDone} / {filesTotal} files", InstallFraction),
            ModInstallState.Done => ("DONE",
                Job.NeedsDownload ? $"{filesTotal} files{(Job.IsCached ? " · from cache" : "")}" : "Nothing to download",
                1),
            ModInstallState.Failed => ("FAILED", Job.Error ?? "", Progress),
            _ => ("CANCELLED", "", 0)
        };
    }
}