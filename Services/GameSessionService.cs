using System;
using System.Diagnostics;
using System.Threading.Tasks;
using KMRLauncherMvvm.Models;

namespace KMRLauncherMvvm.Services;

public class GameSessionService
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
            await instance.LaunchInstanceWithWine(process => _process = process);
        }
        finally
        {
            _process = null;
            Running = null;
            Changed?.Invoke();
        }
    }

    public void Stop()
    {
        if (_process is { } process && Running is { } instance)
            WineLauncher.Stop(process, instance.PrefixPath);
    }
}
