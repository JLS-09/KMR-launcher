using System.Collections.Generic;

namespace KMRLauncherMvvm.Models;

public class InstalledModRecord
{
    public List<string> Files { get; set; } = [];
    public bool IsUserRequested { get; set; }
}