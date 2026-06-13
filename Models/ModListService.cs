using System.Collections.ObjectModel;

namespace KMRLauncherMvvm.Models;

public class ModListService
{
    public ObservableCollection<Mod>? Mods { get; set; }
}