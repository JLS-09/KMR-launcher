using System.Collections.Generic;

namespace KMRLauncherMvvm.Models;

public class ModsCache
{
    public string CurrentCommitHash { get; set; }
    public List<Mod> Mods { get; set; }
}