using System;
using System.Collections.ObjectModel;

namespace KMRLauncherMvvm.Models;

public class ModListService
{
    public ObservableCollection<Mod>? Mods
    {
        get;
        set
        {
            field = value;
            ModsChanged?.Invoke();
        }
    }

    // Raised when a new mod list is set; can come from a background thread (the fetch runs in Task.Run)
    public event Action? ModsChanged;
}