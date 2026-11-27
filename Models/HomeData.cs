using System;
using System.Collections.Generic;

namespace KMRLauncherMvvm.Models;

public class HomeData
{
    public string? Commit { get; set; }
    public DateTimeOffset? CheckedAt { get; set; }
    public List<InstanceCheckResult> Instances { get; set; } = [];
}

public class InstanceCheckResult
{
    public string Folder { get; set; } = "";
    public List<ModUpdateResult> Updates { get; set; } = [];
    public List<ModReplacementResult> Replacements { get; set; } = [];
    public List<string> NotListed { get; set; } = [];
}

public record ModUpdateResult(string Identifier, string Installed, string Version);

public record ModReplacementResult(string Identifier, string Installed, string ReplacedBy, string Version);
