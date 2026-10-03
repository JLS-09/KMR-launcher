using System.Collections.Generic;

namespace KMRLauncherMvvm.Models;

public class ModVersionDurum
{
    public required ModVersion Version { get; set; }
    public HashSet<string> ModsForReason = [];
    public bool IsUserRequested { get; set; }
    
    public string ReasonForAction => IsUserRequested && ModsForReason.Count > 0
        ? $"Requested by user; Dependency of {string.Join(", ", ModsForReason)}"
        : IsUserRequested ? "Requested by user" : $"Dependency of {string.Join(", ", ModsForReason)}";
}