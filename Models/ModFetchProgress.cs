namespace KMRLauncherMvvm.Models;

public class ModFetchProgress
{
    public int ModsReceived { get; set; }
    public int TotalMods { get; set; }
    public string? CurrentModName { get; set; }
    public bool IsCache { get; set; } = false;
    public double Percentage => TotalMods > 0 ? (double)ModsReceived / TotalMods * 100 : 0;
}