using System.Collections.Generic;

namespace KMRLauncherMvvm.Models;

public class Instance(string name, string rootPath)
{
    public string Name { get; set; } = name;
    public string RootPath { get; set; } = rootPath;
    public List<ModVersion> Mods { get; set; } = [];
}