using System;
using System.Threading;
using System.Threading.Tasks;
using KMRLauncherMvvm.Models;

namespace KMRLauncherMvvm.Services.Api;

public interface IModApiService
{
    bool HasCache { get; }

    Task GetAllModsAsync(IProgress<ModFetchProgress>? progress, bool isRefresh, bool cacheOnly = false,
        CancellationToken cancel = default);
}
