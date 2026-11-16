using System.Collections.Generic;

namespace KMRLauncherMvvm.Models;

public enum ModInstallState
{
    Queued,
    Downloading,
    Downloaded,
    Installing,
    Done,
    Failed,
    Cancelled
}

public class ModInstallJob(PlannedModVersion planned)
{
    public PlannedModVersion Planned { get; } = planned;
    public ModVersion Version => Planned.Version;

    public bool NeedsDownload => Version.Kind is null or "package";

    public volatile ModInstallState State = ModInstallState.Queued;
    public volatile bool IsCached;
    public long BytesDone;
    public long? BytesTotal;
    public volatile int FilesDone;
    public volatile int FilesTotal;
    public volatile string? Error;

    public List<string> InstalledFiles { get; } = [];
}