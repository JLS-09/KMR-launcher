using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace KMRLauncherMvvm.Models;

public class Mod
{
    [JsonPropertyName("_id")]
    public required string Id { get; set; }
    
    [JsonPropertyName("name")]
    public required string Name { get; set; }
    
    [JsonPropertyName("abstract")]
    public string Abstract { get; set; }
    
    [JsonPropertyName("author")]
    public List<string> Author { get; set; }
    
    [JsonPropertyName("description")]
    public string Description { get; set; }
    
    [JsonPropertyName("release_status")]
    public ReleaseStatus ReleaseStatus { get; set; }
    
    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; }
    
    [JsonPropertyName("resources")]
    public Resources Resources { get; set; }
    
    public string AuthorsDisplay => string.Join(", ", Author);
}