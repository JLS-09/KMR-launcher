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

    public event Action? ModsChanged;
}