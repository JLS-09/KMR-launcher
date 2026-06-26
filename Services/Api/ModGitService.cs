using System;
using System.IO;
using System.Threading.Tasks;
using KMRLauncherMvvm.Models;
using LibGit2Sharp;
using LibGit2Sharp.Handlers;

namespace KMRLauncherMvvm.Services.Api;

public class ModGitService(ModListService modList) : IModApiService
{
    public async Task GetAllModsAsync(IProgress<ModFetchProgress>? progress, bool isRefresh)
    {
        var applicationBasePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var gitCacheFolder = Path.Combine(applicationBasePath, "kmrLauncher/cache/ckan-meta");

        if (!Directory.Exists(gitCacheFolder))
        {
            Directory.CreateDirectory(Path.Combine(applicationBasePath, "kmrLauncher/cache/ckan-meta"));

            var cloneOptions = new CloneOptions
            {
                OnCheckoutProgress = (path, completedSteps, totalSteps) =>
                {
                    progress?.Report(new ModFetchProgress
                    {
                        TotalMods = totalSteps,
                        ModsReceived = completedSteps,
                        CurrentModName = $"3/3: Checkout {path}"
                    });
                },
                FetchOptions =
                {
                    OnTransferProgress = cloneProgress =>
                    {
                        if (cloneProgress.ReceivedObjects < cloneProgress.TotalObjects)
                        {
                            progress?.Report(new ModFetchProgress
                            {
                                TotalMods = cloneProgress.TotalObjects,
                                ModsReceived = cloneProgress.ReceivedObjects,
                                CurrentModName = "1/3: Cloning CKAN repo"
                            });
                        }
                        else
                        {
                            progress?.Report(new ModFetchProgress
                            {
                                TotalMods = cloneProgress.TotalObjects,
                                ModsReceived = cloneProgress.IndexedObjects,
                                CurrentModName = "2/3: Indexing, please wait..."
                            });
                        }

                        return true;
                    }
                }
            };

            Repository.Clone("https://github.com/KSP-CKAN/CKAN-meta.git", gitCacheFolder, cloneOptions);
        }
        else
        {
            using var repo = new Repository(gitCacheFolder);
            
            var options = new PullOptions
            {
                FetchOptions = new FetchOptions
                {
                    CredentialsProvider = (_, _, _) =>
                        new UsernamePasswordCredentials()
                        {
                            Username = "USERNAME",
                            Password = "PASSWORD"
                        },
                    OnTransferProgress = transferProgress =>
                    {
                        progress?.Report(new ModFetchProgress
                        {
                            TotalMods = transferProgress.TotalObjects,
                            ModsReceived = transferProgress.ReceivedObjects,
                            CurrentModName = "1/3: Pulling CKAN repo"
                        });
                        return true;
                    }
                }
            };

            var signature = new Signature(
                new Identity("MERGE_USER_NAME", "MERGE_USER_EMAIL"), DateTimeOffset.Now);
                
            Commands.Pull(repo, signature, options);
        }


        modList.Mods = [];
    }
}