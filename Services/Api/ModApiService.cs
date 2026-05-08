using System;
using System.Collections.Generic;
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