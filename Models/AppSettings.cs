using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace KMRLauncherMvvm.Models;

public class AppSettings
{
    public ObservableCollection<KspZip> KspZips { get; set; } = [];
    public ObservableCollection<Instance> Instances { get; set; } = [];
    
    public int MaxParallelDownloads { get; set; } = DefaultParallelDownloads;

    // Hex colour, one of ThemeService.AccentChoices
    public string AccentColor { get; set; } = "#DF0105";

    public const int DefaultParallelDownloads = 4;
    public const int MaxAllowedParallelDownloads = 16;
}

public class KspZip
{
    public string Path { get; set; }
    public string RelativeRootPath { get; set; }
    public string Version { get; set; }

    public KspZip(string path, string relativeRootPath, string version)
    {
        Path = path;
        RelativeRootPath = relativeRootPath;
        Version = version;
    }
}