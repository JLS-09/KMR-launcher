using System.Collections.Generic;

namespace KMRLauncherMvvm.Models;

public class PlannedModVersion
{
    public required ModVersion Version { get; set; }
    public HashSet<string> RequiredBy = [];
    public bool IsUserRequested { get; set; }
    public bool IsVersionPickedByUser { get; set; }
    public Relationship? Relationship { get; set; }
    
    public string ReasonForAction => IsUserRequested && RequiredBy.Count > 0
        ? $"Requested by user; Dependency of {string.Join(", ", RequiredBy)}"
        : IsUserRequested ? "Requested by user" : $"Dependency of {string.Join(", ", RequiredBy)}";
}