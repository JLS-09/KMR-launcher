using System;
using System.Collections.Generic;

namespace KMRLauncherMvvm.Models;

public class Instance(string name, string rootPath, string version)
{
    public string Name { get; set; } = name;
    public string RootPath { get; set; } = rootPath;
    public List<ModVersion> Mods { get; set; } = [];
    public string Version { get; set; } =  version;
    public int PlayTime  { get; set; } = 0;
    public DateTime LastPlayed { get; set; }
    public bool IsPrimary { get; set; } = false;
}