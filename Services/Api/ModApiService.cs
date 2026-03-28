using System.Net.Http;
using System.Threading.Tasks;

namespace KMRLauncherMvvm.Services.Api;

public class ModApiService(HttpClient http) : IModApiService
{
    public async Task<string> GetAllModsAsync()
    {
        var response = await http.GetAsync("api/mods");
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();

        return json;
    }
}