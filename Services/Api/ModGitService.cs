using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using KMRLauncherMvvm.Models;
using LibGit2Sharp;

namespace KMRLauncherMvvm.Services.Api;

public class ModGitService(GitHelper gitHelper, ModListService modList) : IModApiService
{
    public async Task GetAllModsAsync(IProgress<ModFetchProgress>? progress, bool isRefresh)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };

        var applicationBasePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var gitCacheFolder = Path.Combine(applicationBasePath, "kmrLauncher/cache/ckan-meta");

        var latestCommitHash = Repository.ListRemoteReferences("https://github.com/KSP-CKAN/CKAN-meta.git")
            .First(r => r.CanonicalName == "refs/heads/master").TargetIdentifier;
        
        var modsCacheFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "kmrLauncher/mods.json");

        if (!Directory.Exists(gitCacheFolder))
            GitHelper.CloneCkanMetaRepo(gitCacheFolder, progress);
        else
        {
            var repo = new Repository(gitCacheFolder);

            var currentCommitHash = repo.Refs.First(r => r.CanonicalName == "refs/heads/master").TargetIdentifier;
            if (currentCommitHash == latestCommitHash && !isRefresh)
            {
                progress?.Report(new ModFetchProgress
                {
                    TotalMods = 1,
                    ModsReceived = 1,
                    CurrentModName = "Loading cache",
                    IsCache = true
                });

                var json = await File.ReadAllTextAsync(modsCacheFile);
                var modsCache = JsonSerializer.Deserialize<ModsCache>(json, options);
                if (modsCache is not null)
                {
                    modList.Mods = new ObservableCollection<Mod>(modsCache.Mods);
                    return;
                }
            }
            else
            {
                GitHelper.PullCkanMetaRepo(gitCacheFolder, progress);
            }
        }

        await gitHelper.PopulateMods(gitCacheFolder, progress);
        var cache = new ModsCache { Mods = [..modList.Mods!], CurrentCommitHash = latestCommitHash };
        var modListJson = JsonSerializer.Serialize(cache, options);
        await File.WriteAllTextAsync(modsCacheFile, modListJson);
    }
}