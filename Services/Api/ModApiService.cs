using System.Threading.Tasks;

namespace KMRLauncherMvvm.Services.Api;

public class ModApiService : IModApiService
{
    public Task<string> GetAllModsAsync()
    {
        return Task.FromResult("Getting all mods");
    }
}