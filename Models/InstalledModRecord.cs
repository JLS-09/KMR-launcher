using System.Collections.Generic;

namespace KMRLauncherMvvm.Models;

public class InstalledModRecord
{
    public List<string> Files { get; set; } = [];
    public bool IsUserRequested { get; set; }

    // Why it was picked when that wasn't directly: "Recommended by X", "Suggested by X", "Supports X"
    public string? Reason { get; set; }
}