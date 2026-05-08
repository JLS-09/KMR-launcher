using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace KMRLauncherMvvm.Models;

public class GetModsResponse
{
    [JsonPropertyName("data")]
    public required List<Mod> Data { get; set; }
    
    [JsonPropertyName("pagination")]
    public required Pagination Pagination { get; set; }
}

public class Pagination
{
    [JsonPropertyName("nextCursor")]
    public required string? NextCursor { get; set; }
    
    [JsonPropertyName("hasNextPage")]
    public required bool HasNextPage { get; set; }
}