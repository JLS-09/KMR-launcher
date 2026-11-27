using System;
using System.Collections.Generic;

namespace KMRLauncherMvvm.Models;

public class HomeData
{
    public string? Commit { get; set; }
    public DateTimeOffset? CheckedAt { get; set; }
    public List<InstanceCheckResult> Instances { get; set; } = [];
    public int UnusedDownloads { get; set; }
    public long UnusedDownloadBytes { get; set; }
    public CkanChanges? CkanChanges { get; set; }
    public string? PopularFor { get; set; }
    public List<PopularMod> Popular { get; set; } = [];
}

public record PopularMod(string Identifier, long Downloads);

public class CkanChanges
{
    public string FromCommit { get; set; } = "";
    public string ToCommit { get; set; } = "";
    public DateTimeOffset? FromSync { get; set; }
    public DateTimeOffset ToSync { get; set; }
    public List<CkanFile> NewVersions { get; set; } = [];
    public List<CkanFile> NewMods { get; set; } = [];
}

public record CkanFile(string Identifier, string Id);

public class InstanceCheckResult
{
    public string Folder { get; set; } = "";
    public List<ModUpdateResult> Updates { get; set; } = [];
    public List<ModReplacementResult> Replacements { get; set; } = [];
    public List<string> NotListed { get; set; } = [];
}

public record ModUpdateResult(string Identifier, string Installed, string Version);

public record ModReplacementResult(string Identifier, string Installed, string ReplacedBy, string Version);
