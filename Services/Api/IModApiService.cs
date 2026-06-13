using System;
using System.Threading.Tasks;
using KMRLauncherMvvm.Models;

namespace KMRLauncherMvvm.Services.Api;

public interface IModApiService
{
    Task GetAllModsAsync(IProgress<ModFetchProgress>? progress);
}