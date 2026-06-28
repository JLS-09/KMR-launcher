using System;
using System.IO;
using System.Threading.Tasks;
using KMRLauncherMvvm.Models;

namespace KMRLauncherMvvm.Services.Api;

public class ModGitService(GitHelper gitHelper) : IModApiService
{
    public async Task GetAllModsAsync(IProgress<ModFetchProgress>? progress, bool isRefresh)
    {
        var applicationBasePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var gitCacheFolder = Path.Combine(applicationBasePath, "kmrLauncher/cache/ckan-meta");

        if (!Directory.Exists(gitCacheFolder))
            GitHelper.CloneCkanMetaRepo(gitCacheFolder, progress);
        else
            GitHelper.PullCkanMetaRepo(gitCacheFolder, progress);

        await gitHelper.PopulateMods(gitCacheFolder, progress);
    }
}