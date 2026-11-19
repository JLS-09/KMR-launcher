using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Humanizer;
using KMRLauncherMvvm.Exceptions;
using KMRLauncherMvvm.Models;
using KMRLauncherMvvm.Services;

namespace KMRLauncherMvvm.ViewModels.InstallMods;

public partial class InstallProgressViewModel : ViewModelBase
{
    private const double DownloadShare = 0.75;

    private readonly InstallService? _installService;
    private readonly InstallRun? _run;
    private readonly int _maxParallelDownloads;
    private readonly CancellationTokenSource _cancel = new();
    private readonly Stopwatch _clock = new();
    private readonly Queue<(TimeSpan Time, long Bytes)> _speedSamples = new();

    public ObservableCollection<ModProgressRowViewModel> Rows { get; } = [];

    [ObservableProperty] private double _overallProgress;
    [ObservableProperty] private string _overallText = "";
    [ObservableProperty] private string _statusText = "STARTING";
    [ObservableProperty] private string _percentText = "0%";
    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private bool _isFinished;
    [ObservableProperty] private bool _hasError;
    [ObservableProperty] private string _errorMessage = "";

    [ObservableProperty] [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    private bool _canCancel;

    public event Action? CloseRequested;

    public InstallProgressViewModel(InstallService installService, InstallRun run, int maxParallelDownloads)
    {
        _installService = installService;
        _run = run;
        _maxParallelDownloads = maxParallelDownloads;
        foreach (var job in run.Jobs) Rows.Add(new ModProgressRowViewModel(job));
        Refresh(TimeSpan.Zero);
    }

    public InstallProgressViewModel()
    {
    }

    public async Task<bool> RunAsync()
    {
        if (_installService is null || _run is null) return false;

        IsRunning = true;
        CanCancel = true;
        StatusText = "INSTALLING";
        _clock.Start();
        var timer = new DispatcherTimer(TimeSpan.FromMilliseconds(100), DispatcherPriority.Background,
            (_, _) => Refresh(_clock.Elapsed));
        timer.Start();

        var succeeded = false;
        try
        {
            await _installService.RunAsync(_run, _maxParallelDownloads, _cancel.Token);
            succeeded = true;
            StatusText = "DONE";
        }
        catch (OperationCanceledException e)
        {
            StatusText = "CANCELLED";
            ShowError(e.Message.StartsWith("The install was cancelled")
                ? e.Message + " Nothing was changed."
                : e.Message);
        }
        catch (ModInstallException e)
        {
            StatusText = "FAILED";
            ShowError(e.Message + "\n\nNothing was changed in the instance.");
        }
        catch (Exception e)
        {
            StatusText = "FAILED";
            ShowError($"Unexpected error: {e.Message}");
        }
        finally
        {
            timer.Stop();
            _clock.Stop();
            Refresh(_clock.Elapsed);
            IsRunning = false;
            CanCancel = false;
            IsFinished = true;
        }

        return succeeded;
    }

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel()
    {
        CanCancel = false;
        StatusText = "CANCELLING";
        _cancel.Cancel();
    }

    [RelayCommand]
    private void Close() => CloseRequested?.Invoke();

    private void ShowError(string message)
    {
        ErrorMessage = message;
        HasError = true;
    }

    public void Refresh(TimeSpan elapsed)
    {
        foreach (var row in Rows) row.Update();
        if (Rows.Count == 0)
        {
            OverallProgress = IsFinished && !HasError ? 1 : 0;
            OverallText = "No mods to install";
            PercentText = $"{OverallProgress:0%}";
            return;
        }

        OverallProgress = Rows.Average(r => r.Job.NeedsDownload
            ? DownloadShare * r.DownloadFraction + (1 - DownloadShare) * r.InstallFraction
            : r.IsDone ? 1 : 0);
        PercentText = $"{Math.Floor(OverallProgress * 100)}%";

        var downloads = Rows.Where(r => r.Job.NeedsDownload && !r.Job.IsCached).Select(r => r.Job).ToList();
        var bytesDone = downloads.Sum(j => Interlocked.Read(ref j.BytesDone));
        var bytesTotal = downloads.Sum(j => j.BytesTotal ?? j.Version.DownloadSize ?? 0);
        var done = Rows.Count(r => r.IsDone);

        var parts = new List<string> { $"{done} / {Rows.Count} mods" };
        if (bytesTotal > 0) parts.Add($"{bytesDone.Bytes().Humanize("0.#")} / {bytesTotal.Bytes().Humanize("0.#")}");
        var speed = Speed(elapsed, bytesDone);
        if (IsRunning && speed > 0) parts.Add($"{((long)speed).Bytes().Humanize("0.#")}/s");
        OverallText = string.Join(" · ", parts);
    }

    private double Speed(TimeSpan now, long bytes)
    {
        _speedSamples.Enqueue((now, bytes));
        while (_speedSamples.Count > 1 && now - _speedSamples.Peek().Time > TimeSpan.FromSeconds(2))
            _speedSamples.Dequeue();

        var (time, oldBytes) = _speedSamples.Peek();
        var seconds = (now - time).TotalSeconds;
        return seconds > 0.2 ? (bytes - oldBytes) / seconds : 0;
    }
}