using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace KMRLauncherMvvm.Models;

public class Mod
{
    [JsonPropertyName("_id")]
    public required string Id { get; set; }
    
    [JsonPropertyName("name")]
    public required string Name { get; set; }
    
    [JsonPropertyName("abstract")]
    public required string Abstract { get; set; }
    
    [JsonPropertyName("author")]
    public required List<string> Author { get; set; }
    
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("release_status")]
    public ReleaseStatus ReleaseStatus { get; set; } = ReleaseStatus.Stable;
    
    [JsonPropertyName("tags")]
    public List<string>? Tags { get; set; }
    
    [JsonPropertyName("resources")]
    public Resources? Resources { get; set; }
    
    [JsonPropertyName("versions")] public List<ModVersion> Versions { get; set; } = [];

    public ModVersion? LatestVersion => Versions.FirstOrDefault();
    public string AuthorsDisplay => string.Join(", ", Author);

    public override string ToString()
    {
        return $"{{ Id: \"{Id}\", Name: \"{Name}\", Abstract: \"{Abstract}\", Author: [\"{string.Join("\", \"", Author)}\"], " +
               $"Description: {(Description is not null ? $"\"{Description}\"" : "null")}, ReleaseStatus: {ReleaseStatus}, " +
               $"Tags: {(Tags is not null ? $"[\"{string.Join("\", \"", Tags)}\"]" : "null")}, " +
               $"Resources: {(Resources is not null ? Resources : "null" )}, Versions: {string.Join(", ", Versions)} }}";
    }
}