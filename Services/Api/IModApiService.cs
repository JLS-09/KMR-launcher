using System.Collections.Generic;
using System.Threading.Tasks;
using KMRLauncherMvvm.Models;

namespace KMRLauncherMvvm.Services.Api;

public interface IModApiService
{
    Task<GetModsResponse> GetModsAsync(int pageSize, string? cursor = null, string? modFilter = null, string? authorFilter = null);
    Task<List<Mod>> GetAllModsAsync();
    Task<List<ModVersion>> GetVersionsByModIdAsync(string modId);
}