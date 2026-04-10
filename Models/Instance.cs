using System.Collections.Generic;

namespace KMRLauncherMvvm.Models;

public class Instance
{
    public string Name { get; set; }
    public string RootPath { get; set; }
    public List<ModVersion> Mods { get; set; }
}