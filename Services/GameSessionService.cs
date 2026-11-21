using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using KMRLauncherMvvm.Models;

namespace KMRLauncherMvvm.Services;

public class GameSessionService(InstanceService instances)
{
    public Instance? Running { get; private set; }
    public DateTime StartedAt { get; private set; }
    public bool IsRunning => Running is not null;

    private Process? _process;

    public event Action? Changed;

    public async Task LaunchAsync(Instance instance)
    {
        if (IsRunning) return;

        Running = instance;
        StartedAt = DateTime.Now;
        Changed?.Invoke();

        try
        {
            await RunAsync(instance);
        }
        finally
        {
            _process = null;
            Running = null;
            Changed?.Invoke();
        }
    }

    private async Task RunAsync(Instance instance)
    {
        if (string.IsNullOrWhiteSpace(instance.PrefixPath))
            instance.PrefixPath = WinePrefixes.DefaultFor(instance, instances.Instances);

        var process = WineLauncher.Launch(instance.PrefixPath, Path.Combine(instance.RootPath, "KSP_x64.exe"),
            Instance.SplitArguments(instance.LaunchArguments), instances.Settings.WineDebug);
        _process = process;
        var started = process.StartTime;

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null) Console.WriteLine(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null) Console.Error.WriteLine(e.Data);
        };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync();

        instances.RecordPlay(instance, started, process.ExitTime);
    }

    public void Stop()
    {
        if (_process is { } process && Running is { } instance)
            WineLauncher.Stop(process, instance.PrefixPath);
    }
}
