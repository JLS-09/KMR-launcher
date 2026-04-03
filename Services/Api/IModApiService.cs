using System.Collections.Generic;
using System.Threading.Tasks;
using KMRLauncherMvvm.Models;

namespace KMRLauncherMvvm.Services.Api;

public interface IModApiService
{
    Task<List<Mod>> GetModsAsync(int page, int pageSize);
    Task<List<ModVersion>> GetVersionsByModIdAsync(string modId);
}