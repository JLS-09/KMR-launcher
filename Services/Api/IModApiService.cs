using System.Threading.Tasks;

namespace KMRLauncherMvvm.Services.Api;

public interface IModApiService
{
    Task<string> GetAllModsAsync();
}