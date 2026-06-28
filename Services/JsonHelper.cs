using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using KMRLauncherMvvm.Models;

namespace KMRLauncherMvvm.Services;

public static class JsonHelper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };
    
    public static ModVersion? ParseVersion(string ckanFilePath, string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);

            var id = Path.GetFileNameWithoutExtension(ckanFilePath);

            var v = JsonSerializer.Deserialize<ModVersion>(json, JsonOptions);
            if (v is null) return null;

            v.Id = id;
            return v;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[WARN] Could not parse version {ckanFilePath}: {ex.Message}");
            return null;
        }
    }

    public static Mod? ParseMod(string latestJson, List<ModVersion> versions)
    {
        try
        {
            using var doc = JsonDocument.Parse(latestJson);
            var root = doc.RootElement;

            if (!root.TryGetProperty("name", out var nameProp) ||
                !root.TryGetProperty("abstract", out var abstractProp) ||
                !root.TryGetProperty("identifier", out var idProp))
                return null;

            List<string> authors = [];
            if (root.TryGetProperty("author", out var authorEl))
            {
                authors = authorEl.ValueKind == JsonValueKind.Array
                    ? JsonSerializer.Deserialize<List<string>>(authorEl.GetRawText(), JsonOptions)!
                    : [authorEl.GetString()!];
            }

            var releaseStatus = ReleaseStatus.Stable;
            if (root.TryGetProperty("release_status", out var rsProp))
                Enum.TryParse(rsProp.GetString(), ignoreCase: true, out releaseStatus);

            return new Mod
            {
                Id = idProp.GetString()!,
                Name = nameProp.GetString()!,
                Abstract = abstractProp.GetString()!,
                Author = authors,
                Description = root.TryGetProperty("description", out var d) ? d.GetString() : null,
                Tags = root.TryGetProperty("tags", out var tags)
                    ? JsonSerializer.Deserialize<List<string>>(tags.GetRawText(), JsonOptions)
                    : null,
                Resources = root.TryGetProperty("resources", out var res)
                    ? JsonSerializer.Deserialize<Resources>(res.GetRawText(), JsonOptions)
                    : null,
                ReleaseStatus = releaseStatus,
                Versions = versions,
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[WARN] Could not parse mod from latest version: {ex.Message}");
            return null;
        }
    }
}