using System;
using System.Threading.Tasks;
using KMRLauncherMvvm.Models;

namespace KMRLauncherMvvm.Services;

// The one place that launches KSP, so the sidebar and the Instances page agree on whether a game runs.
// Log and Stop come with the Running card (Phase 5 task 5).
public class GameSessionService
{
    public Instance? Running { get; private set; }
    public DateTime StartedAt { get; private set; }
    public bool IsRunning => Running is not null;

    // Raised on the UI thread when a game starts or exits
    public event Action? Changed;

    // Throws when the game can't be started (e.g. wine isn't installed)
    public async Task LaunchAsync(Instance instance)
    {
        if (IsRunning) return;

        Running = instance;
        StartedAt = DateTime.Now;
        Changed?.Invoke();

        try
        {
            await instance.LaunchInstanceWithWine();
        }
        finally
        {
            Running = null;
            Changed?.Invoke();
        }
    }
}
