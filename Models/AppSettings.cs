using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace KMRLauncherMvvm.Models;

public class AppSettings
{
    public ObservableCollection<KspZip> KspZips { get; set; } = [];
    public ObservableCollection<Instance> Instances { get; set; } = [];
}

public class KspZip
{
    public string Path { get; set; }
    public string Version { get; set; }

    public KspZip(string path, string version)
    {
        Path = path;
        Version = version;
    }
}