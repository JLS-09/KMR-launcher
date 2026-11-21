using System;
using System.Text.Json.Serialization;

namespace KMRLauncherMvvm.Models;

public class SyncStatus
{
    public DateTimeOffset? LastFetch { get; set; }
    public string? LastCommit { get; set; }
    public DateTimeOffset? LastFailure { get; set; }
    public string? LastError { get; set; }

    [JsonIgnore]
    public bool LastAttemptFailed => LastFailure is { } failure && (LastFetch is not { } fetch || failure > fetch);
}
