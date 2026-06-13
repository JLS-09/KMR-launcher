using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KMRLauncherMvvm.Models;

namespace KMRLauncherMvvm.Services.Api;

public interface IModApiService
{
    Task<List<Mod>> GetAllModsAsync(IProgress<ModFetchProgress>? progress);
}