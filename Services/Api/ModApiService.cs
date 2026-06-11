using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
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

    public async Task<List<Mod>> GetAllModsAsync()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };

        await using var stream = await http.GetStreamAsync("api/mods/all");
        
        try {
            return (await JsonSerializer.DeserializeAsync<List<Mod>>(stream, options))!;
        } catch (JsonException ex) {
            Console.WriteLine($"Path: {ex.Path}, LineNumber: {ex.LineNumber}");
            Console.WriteLine(ex.Message);
            throw;
        }
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