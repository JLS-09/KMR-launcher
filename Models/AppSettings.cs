using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json.Serialization;

namespace KMRLauncherMvvm.Models;

public class AppSettings
{
    public ObservableCollection<KspZip> KspZips { get; set; } = [];

    public List<string> InstanceFolders { get; set; } = [];
    public string? PrimaryInstance { get; set; }

    [JsonIgnore] public ObservableCollection<Instance> Instances { get; set; } = [];
    [JsonIgnore] public IEnumerable<Instance> UsableInstances => Instances.Where(i => !i.IsMissing);

    [JsonIgnore] public bool UsesLegacyFile { get; set; }
    [JsonIgnore] public string? LoadProblem { get; set; }

    public int MaxParallelDownloads { get; set; } = DefaultParallelDownloads;

    public bool FetchOnStartup { get; set; } = true;

    public string AccentColor { get; set; } = "#DF0105";

    public const int DefaultParallelDownloads = 4;
    public const int MaxAllowedParallelDownloads = 16;
}

public class LegacySettingsFile
{
    public ObservableCollection<KspZip> KspZips { get; set; } = [];
    public List<Instance> Instances { get; set; } = [];
    public int MaxParallelDownloads { get; set; } = AppSettings.DefaultParallelDownloads;
    public string AccentColor { get; set; } = "#DF0105";

    public static LegacySettingsFile From(AppSettings settings) => new()
    {
        KspZips = settings.KspZips,
        Instances = [.. settings.Instances],
        MaxParallelDownloads = settings.MaxParallelDownloads,
        AccentColor = settings.AccentColor
    };
}

public class KspZip
{
    public string Path { get; set; }
    public string RelativeRootPath { get; set; }
    public string Version { get; set; }

    public KspZip(string path, string relativeRootPath, string version)
    {
        Path = path;
        RelativeRootPath = relativeRootPath;
        Version = version;
    }
}