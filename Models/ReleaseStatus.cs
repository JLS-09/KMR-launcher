using System.Text.Json.Serialization;

namespace KMRLauncherMvvm.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ReleaseStatus
{
    Stable,
    Testing,
    Development
}