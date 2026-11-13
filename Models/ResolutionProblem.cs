namespace KMRLauncherMvvm.Models;

public sealed class ResolutionProblem
{
    public required string Mod { get; init; }
    public Relationship? Relationship { get; init; }
    public required string Message { get; init; }
    public bool IsConflict { get; init; }
}