using System.Net.Http;
using System.Threading.Tasks;

namespace KMRLauncherMvvm.Services.Api;

public class ModApiService(HttpClient http) : IModApiService
{
    public async Task<string> GetModsAsync(int page, int pageSize)
    {
        var response = await http.GetAsync($"api/mods?page={page}&page_size={pageSize}");
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();

        return json;
    }
}