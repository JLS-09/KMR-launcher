using System.Collections.Generic;

namespace KMRLauncherMvvm.Models;

public class Instance
{
    public string Name { get; set; }
    public string Path { get; set; }
    public List<ModVersion> Mods { get; set; }
}