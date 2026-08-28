using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Humanizer;

namespace KMRLauncherMvvm.Models;

public class ModVersion
{
    public string? Id { get; set; }
    [JsonPropertyName("identifier")] public required string Identifier { get; set; }
    [JsonPropertyName("version")] public required string Version { get; set; }

    [JsonPropertyName("spec_version")]
    [JsonConverter(typeof(StringOrIntConverter))]
    public required string SpecVersion { get; set; }

    [JsonPropertyName("ksp_version")] public string? KspVersion { get; set; }
    [JsonPropertyName("ksp_version_min")] public string? KspVersionMin { get; set; }
    [JsonPropertyName("ksp_version_max")] public string? KspVersionMax { get; set; }

    [JsonPropertyName("ksp_version_strict")]
    public bool KspVersionStrict { get; set; }

    [JsonPropertyName("license")]
    [JsonConverter(typeof(StringOrArrayConverter))]
    public required List<string> License { get; set; }

    [JsonPropertyName("download")]
    [JsonConverter(typeof(StringOrArrayConverter))]
    public List<string>? Download { get; set; }

    [JsonPropertyName("download_hash")] public DownloadHash? DownloadHash { get; set; }
    [JsonPropertyName("download_size")] public int? DownloadSize { get; set; }

    [JsonPropertyName("download_content_type")]
    public string? DownloadContentType { get; set; }

    [JsonPropertyName("install_size")]
    [JsonConverter(typeof(InstallSizeConverter))]
    public double? InstallSize { get; set; }

    [JsonPropertyName("release_date")]
    [JsonConverter(typeof(MongoDateConverter))]
    public DateTime? ReleaseDate { get; set; }

    [JsonPropertyName("kind")] public string? Kind { get; set; }
    [JsonPropertyName("localizations")] public List<string>? Localizations { get; set; }
    [JsonPropertyName("provides")] public List<string>? Provides { get; set; }

    [JsonPropertyName("install")] public List<InstallDirective>? Install { get; set; }
    [JsonPropertyName("depends")] public List<Relationship>? Depends { get; set; }
    [JsonPropertyName("recommends")] public List<Relationship>? Recommends { get; set; }
    [JsonPropertyName("suggests")] public List<Relationship>? Suggests { get; set; }
    [JsonPropertyName("supports")] public List<Relationship>? Supports { get; set; }
    [JsonPropertyName("conflicts")] public List<Relationship>? Conflicts { get; set; }
    [JsonPropertyName("replaced_by")] public JsonElement? ReplacedBy { get; set; }

    [JsonIgnore] public string LastUpdatedHumanized => ReleaseDate is null ? "" : ReleaseDate.Humanize();

    [JsonIgnore]
    public string ReasonForAction => ModsForReason.Count == 0
        ? "Requested by user"
        : $"Dependency of {string.Join(", ", ModsForReason)}";

    [JsonIgnore] public List<string> ModsForReason = [];

    public override string ToString()
    {
        return $"{{ Id: \"{Id}\", Identifier: \"{Identifier}\", Version: \"{Version}\", SpecVersion: \"{SpecVersion}\", " +
               $"KspVersion: {(KspVersion is not null ? $"\"{KspVersion}\"" : "null")}, " +
               $"KspVersionMin: {(KspVersionMin is not null ? $"\"{KspVersionMin}\"" : "null")}, " +
               $"KspVersionMax: {(KspVersionMax is not null ? $"\"{KspVersionMax}\"" : "null")}, KspVersionStrict: \"{KspVersionStrict}\", " +
               $"License: [\"{string.Join("\", \"", License)}\"], Download: {(Download is not null ? $"[\"{string.Join("\", \"", Download)}\"]" : "null")}, " +
               $"DownloadHash: {DownloadHash}, DownloadSize: {(DownloadSize is not null ? $"{DownloadSize}" : "null")}, " +
               $"DownloadContentType: {(DownloadContentType is not null ? $"\"{DownloadContentType}\"" : "null")}, " +
               $"InstallSize: {(InstallSize is not null ? $"{InstallSize}" : "null")}, " +
               $"ReleaseDate: {(ReleaseDate is not null ? $"\"{ReleaseDate}\"" : "null")}, " +
               $"Kind: {(Kind is not null ? $"\"{Kind}\"" : "null")}, " +
               $"Localizations: {(Localizations is not null ? $"[\"{string.Join("\", \"", Localizations)}\"]" : "null")}, " +
               $"Provides: {(Provides is not null ? $"[{string.Join(", ", Provides)}]" : "null")}, " +
               $"Install: {(Install is not null ? $"[{string.Join(", ", Install)}]" : "null")}, " +
               $"Depends: {(Depends is not null ? $"[{string.Join(", ", Depends)}]" : "null")}, " +
               $"Recommends: {(Recommends is not null ? $"[{string.Join(", ", Recommends)}]" : "null")}, " +
               $"Suggests: {(Suggests is not null ? $"[{string.Join(", ", Suggests)}]" : "null")}, " +
               $"Conflicts: {(Conflicts is not null ? $"[{string.Join(", ", Conflicts)}]" : "null")}, " +
               $"ReplacedBy: {(ReplacedBy is not null ? ReplacedBy : "null")} }}";
    }
}

public class DownloadHash
{
    [JsonPropertyName("sha1")] public string? Sha1 { get; set; }
    [JsonPropertyName("sha256")] public string? Sha256 { get; set; }

    public override string ToString()
    {
        return $"{{ Sha1: {(Sha1 is not null ? $"\"{Sha1}\"" : "null")}, " +
               $"Sha256: {(Sha256 is not null ? $"\"{Sha256}\"" : "null")} }}";
    }
}

public class InstallDirective
{
    [JsonPropertyName("install_to")] public required string InstallTo { get; set; }
    [JsonPropertyName("file")] public string? File { get; set; }
    [JsonPropertyName("find")] public string? Find { get; set; }
    [JsonPropertyName("find_regexp")] public string? FindRegexp { get; set; }

    [JsonPropertyName("find_matches_file")]
    public bool? FindMatchesFile { get; set; }

    [JsonPropertyName("find_matches_files")]
    public bool? FindMatchesFiles { get; set; }

    [JsonPropertyName("as")] public string? As { get; set; }

    [JsonPropertyName("filter")]
    [JsonConverter(typeof(StringOrArrayConverter))]
    public List<string>? Filter { get; set; }

    [JsonPropertyName("filter_regexp")]
    [JsonConverter(typeof(StringOrArrayConverter))]
    public List<string>? FilterRegexp { get; set; }

    [JsonPropertyName("include_only")] public List<string>? IncludeOnly { get; set; }
    [JsonPropertyName("comment")] public string? Comment { get; set; }

    public override string ToString()
    {
        return $"{{ InstallTo: \"{InstallTo}\", File: {(File is not null ? $"\"{File}\"" : "null")}, " +
               $"Find: {(Find is not null ? $"\"{Find}\"" : "null")}, " +
               $"FindRegexp: {(FindRegexp is not null ? $"\"{FindRegexp}\"" : "null")}, " +
               $"FindMatchesFile: {(FindMatchesFile is not null ? $"{FindMatchesFile}" : "null")}, " +
               $"FindMatchesFiles: {(FindMatchesFiles is not null ? $"{FindMatchesFiles}" : "null")}, " +
               $"As: {(As is not null ? $"\"{As}\"" : "null")}, " +
               $"Filter: {(Filter is not null ? $"[\"{string.Join("\", \"", Filter)}\"]" : "null")}, " +
               $"FilterRegexp: {(FilterRegexp is not null ? $"[\"{string.Join("\", \"", FilterRegexp)}\"]" : "null")}, " +
               $"IncludeOnly: {(IncludeOnly is not null ? $"[\"{string.Join("\", \"", IncludeOnly)}\"]" : "null")}, " +
               $"Comment: {(Comment is not null ? $"\"{Comment}\"" : "null")} }}";
    }
}

public class Relationship
{
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("version")] public string? Version { get; set; }
    [JsonPropertyName("min_version")] public string? MinVersion { get; set; }
    [JsonPropertyName("max_version")] public string? MaxVersion { get; set; }

    [JsonPropertyName("suppress_recommendations")]
    public bool? SuppressRecommendations { get; set; }

    [JsonPropertyName("any_of")] public List<AnyOfEntry>? AnyOf { get; set; }
    [JsonPropertyName("comment")] public string? Comment { get; set; }

    public override string ToString()
    {
        return
            $"{{ Name: {(Name is not null ? $"\"{Name}\"" : "null")}, Version: {(Version is not null ? $"\"{Version}\"" : "null")}, " +
            $"MinVersion: {(MinVersion is not null ? $"\"{(MinVersion is not null ? $"\"{MinVersion}\"" : "null")}\"" : "null")}, " +
            $"MaxVersion: {(MaxVersion is not null ? $"\"{(MaxVersion is not null ? $"\"{MaxVersion}\"" : "null")}\"" : "null")}, " +
            $"SuppressRecommendations: {(SuppressRecommendations is not null ? $"{SuppressRecommendations}" : "null")}, " +
            $"Comment: {(Comment is not null ? $"\"{Comment}\"" : "null")}, " +
            $"AnyOf: {(AnyOf is not null ? $"{AnyOf}" : "null")} }}";
    }
}

public class AnyOfEntry
{
    [JsonPropertyName("name")] public required string Name { get; set; }
    [JsonPropertyName("version")] public string? Version { get; set; }
    [JsonPropertyName("min_version")] public string? MinVersion { get; set; }
    [JsonPropertyName("max_version")] public string? MaxVersion { get; set; }

    public override string ToString()
    {
        return $"{{ Name: \"{Name}\", Version: {(Version is not null ? $"\"{Version}\"" : "null")},  " +
               $"MinVersion: {(MinVersion is not null ? $"\"{MinVersion}\"" : "null")}, " +
               $"MaxVersion: {(MaxVersion is not null ? $"\"{MaxVersion}\"" : "null")} }}";
    }
}

public class StringOrArrayConverter : JsonConverter<List<string>>
{
    public override List<string> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
            return [reader.GetString()!];

        if (reader.TokenType == JsonTokenType.StartArray)
            return JsonSerializer.Deserialize<List<string>>(ref reader, options)!;

        if (reader.TokenType == JsonTokenType.Null)
            return null!;

        throw new JsonException($"Cannot convert {reader.TokenType} to List<string>");
    }

    public override void Write(Utf8JsonWriter writer, List<string> value, JsonSerializerOptions options)
        => JsonSerializer.Serialize(writer, value, options);
}

public class StringOrIntConverter : JsonConverter<string>
{
    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString()!,
            JsonTokenType.Number => reader.GetInt64().ToString(),
            _ => throw new JsonException($"Cannot convert {reader.TokenType} to string")
        };
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
        => writer.WriteStringValue(value);
}

public class InstallSizeConverter : JsonConverter<double?>
{
    public override double? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return null;
        if (reader.TokenType == JsonTokenType.Number) return reader.GetDouble();

        if (reader.TokenType == JsonTokenType.StartObject)
        {
            using var doc = JsonDocument.ParseValue(ref reader);
            if (doc.RootElement.TryGetProperty("$numberDouble", out var val))
                return val.GetString() switch
                {
                    "Infinity" => double.PositiveInfinity,
                    "-Infinity" => double.NegativeInfinity,
                    "NaN" => double.NaN,
                    _ => throw new JsonException("Unknown $numberDouble value")
                };
        }

        throw new JsonException($"Cannot convert {reader.TokenType} to double?");
    }

    public override void Write(Utf8JsonWriter writer, double? value, JsonSerializerOptions options)
    {
        if (value is null) writer.WriteNullValue();
        else writer.WriteNumberValue(value.Value);
    }
}

public class MongoDateConverter : JsonConverter<DateTime?>
{
    public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        if (reader.TokenType == JsonTokenType.String)
            return DateTime.Parse(reader.GetString()!, null,
                System.Globalization.DateTimeStyles.RoundtripKind);

        if (reader.TokenType == JsonTokenType.StartObject)
        {
            using var doc = JsonDocument.ParseValue(ref reader);
            if (doc.RootElement.TryGetProperty("$date", out var dateEl))
                return DateTime.Parse(dateEl.GetString()!, null,
                    System.Globalization.DateTimeStyles.RoundtripKind);

            throw new JsonException("Expected { \"$date\": \"...\" } object");
        }

        throw new JsonException($"Cannot convert token type {reader.TokenType} to DateTime?");
    }

    public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartObject();
        writer.WriteString("$date", value.Value.ToString("O"));
        writer.WriteEndObject();
    }
}