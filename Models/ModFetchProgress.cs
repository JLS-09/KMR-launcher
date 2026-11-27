namespace KMRLauncherMvvm.Models;

public enum FetchPhase
{
    Receiving,
    Indexing,
    CheckingOut,
    Reading
}

public class ModFetchProgress
{
    public int ModsReceived { get; set; }
    public int TotalMods { get; set; }
    public string? CurrentModName { get; set; }
    public bool IsCache { get; set; } = false;
    public FetchPhase Phase { get; set; } = FetchPhase.Reading;
    public long ReceivedBytes { get; set; }
}
