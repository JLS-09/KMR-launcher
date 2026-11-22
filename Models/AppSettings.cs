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
    public string? InstanceParentFolder { get; set; }

    [JsonIgnore] public ObservableCollection<Instance> Instances { get; set; } = [];
    [JsonIgnore] public IEnumerable<Instance> UsableInstances => Instances.Where(i => !i.IsMissing);

    [JsonIgnore] public string? LoadProblem { get; set; }

    public int MaxParallelDownloads { get; set; } = DefaultParallelDownloads;

    public bool FetchOnStartup { get; set; } = true;

    public WineDebugLevel WineDebug { get; set; } = WineDebugLevel.ErrorsOnly;

    public string AccentColor { get; set; } = "#DF0105";

    public const int DefaultParallelDownloads = 4;
    public const int MaxAllowedParallelDownloads = 16;
}

[JsonConverter(typeof(JsonStringEnumConverter<WineDebugLevel>))]
public enum WineDebugLevel
{
    Off,
    ErrorsOnly,
    WineDefault
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