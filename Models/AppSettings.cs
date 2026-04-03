using System.Collections.Generic;

namespace KMRLauncherMvvm.Models;

public class AppSettings
{
    public string KspZipLocation { get; set; }
    public List<Instance> Instances { get; set; } = [];
}