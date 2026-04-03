using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using KMRLauncherMvvm.Models;

namespace KMRLauncherMvvm.Services.Api;

public class ModApiService(HttpClient http) : IModApiService
{
    public async Task<List<Mod>> GetModsAsync(int page, int pageSize)
    {
        var response = await http.GetAsync($"api/mods?page={page}&page_size={pageSize}");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };
        
        return JsonSerializer.Deserialize<List<Mod>>(json, options)!;
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