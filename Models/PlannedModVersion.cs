using System.Collections.Generic;

namespace KMRLauncherMvvm.Models;

public class PlannedModVersion
{
    public required ModVersion Version { get; set; }
    public HashSet<string> RequiredBy = [];
    public bool IsUserRequested { get; set; }
    public bool IsVersionPickedByUser { get; set; }
    public Relationship? Relationship { get; set; }
    public string? OptionalReason { get; set; }

    public string ReasonForAction
    {
        get
        {
            List<string> reasons = [];
            if (OptionalReason is not null) reasons.Add(OptionalReason);
            else if (IsUserRequested) reasons.Add("Requested by user");
            if (RequiredBy.Count > 0) reasons.Add($"Dependency of {string.Join(", ", RequiredBy)}");
            return string.Join("; ", reasons);
        }
    }
}