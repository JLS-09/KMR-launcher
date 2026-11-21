using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using KMRLauncherMvvm.Services;

namespace KMRLauncherMvvm.Models;

public class Instance(string name, string rootPath, string version)
{
    public string Name { get; set; } = name;
    public string RootPath { get; set; } = rootPath;
    public List<ModVersion> Mods { get; set; } = [];
    public Dictionary<string, InstalledModRecord> ModRecords { get; set; } = [];
    public string Version { get; set; } = version;
    public List<string> CompatibleKspVersions { get; set; } = [];
    public string LaunchArguments { get; set; } = "";

    public string PrefixPath { get; set; } = "";
    public int PlayTime { get; set; }
    public DateTime LastPlayed { get; set; }
    public bool IsPrimary { get; set; }

    [JsonIgnore] public string? LoadError { get; set; }
    [JsonIgnore] public Dictionary<string, string> UnmanagedDlls { get; set; } = [];
    [JsonIgnore] public bool IsMissing => LoadError is not null;

    [JsonIgnore] public string CompatibleSummary => KspVersions.DescribeCompatible(this);

    public static string[] SplitArguments(string arguments)
    {
        var result = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;
        var hasArgument = false;

        foreach (var c in arguments)
        {
            if (c == '"')
            {
                inQuotes = !inQuotes;
                hasArgument = true;
            }
            else if (char.IsWhiteSpace(c) && !inQuotes)
            {
                if (hasArgument) result.Add(current.ToString());
                current.Clear();
                hasArgument = false;
            }
            else
            {
                current.Append(c);
                hasArgument = true;
            }
        }

        if (hasArgument) result.Add(current.ToString());
        return [.. result];
    }
}
