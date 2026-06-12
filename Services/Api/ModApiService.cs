using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using KMRLauncherMvvm.Models;

namespace KMRLauncherMvvm.Services.Api;

public class ModApiService(HttpClient http) : IModApiService
{
    public async Task<GetModsResponse> GetModsAsync(
        int pageSize, 
        string? cursor = null, 
        string? modFilter = null, 
        string? authorFilter = null
        )
    {
        var response = await http.GetAsync(
            $"api/mods?page_size={pageSize}" +
            $"{(cursor is not null ? $"&cursor={cursor}" : "")}" +
            $"{(modFilter is not null ? $"&mod_filter={modFilter}" : "")}" +
            $"{(authorFilter is not null ? $"&author_filter={authorFilter}" : "")}"
            );
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };
        
        return JsonSerializer.Deserialize<GetModsResponse>(json, options)!;
    }

    public async Task<List<Mod>> GetAllModsAsync(IProgress<double>? progress = null)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };

        using var response = await http.GetAsync("api/mods/all", HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        var total = 0;
        if (response.Headers.TryGetValues("X-Total-Count", out var values))
        {
            int.TryParse(values.First(), out total);
        }

        var mods = new List<Mod>();
        var received = 0;

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream);

        while (await reader.ReadLineAsync() is { } line)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            try
            {
                var mod = JsonSerializer.Deserialize<Mod>(line, options);
                if (mod != null)
                    mods.Add(mod);
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"Path: {ex.Path}, LineNumber: {ex.LineNumber}");
                Console.WriteLine(ex.Message);
                throw;
            }

            received++;
            if (total > 0)
            {
                progress?.Report(((double)received / total) * 100);
            }
        }
        return mods;
    }

    public async Task<List<ModVersion>> GetVersionsByModIdAsync(string modId)
    {
        var response = await http.GetAsync($"api/mods/{modId}/versions");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        
        return JsonSerializer.Deserialize<List<ModVersion>>(json, options)!;
    }
}