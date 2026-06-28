using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using KMRLauncherMvvm.Models;
using LibGit2Sharp;

namespace KMRLauncherMvvm.Services;

public class GitHelper(ModListService modList)
{
    public static void CloneCkanMetaRepo(string gitCacheFolder, IProgress<ModFetchProgress>? progress)
    {
        Directory.CreateDirectory(gitCacheFolder);

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

    public static void PullCkanMetaRepo(string gitCacheFolder, IProgress<ModFetchProgress>? progress)
    {
        using var repo = new Repository(gitCacheFolder);

        var options = new PullOptions
        {
            FetchOptions = new FetchOptions
            {
                CredentialsProvider = (_, _, _) =>
                    new UsernamePasswordCredentials
                    {
                        Username = "USERNAME",
                        Password = "PASSWORD"
                    },
                OnTransferProgress = transferProgress =>
                {
                    if (transferProgress.ReceivedObjects < transferProgress.TotalObjects)
                    {
                        progress?.Report(new ModFetchProgress
                        {
                            TotalMods = transferProgress.TotalObjects,
                            ModsReceived = transferProgress.ReceivedObjects,
                            CurrentModName = "1/2: Cloning CKAN repo"
                        });
                    }
                    else
                    {
                        progress?.Report(new ModFetchProgress
                        {
                            TotalMods = transferProgress.TotalObjects,
                            ModsReceived = transferProgress.IndexedObjects,
                            CurrentModName = "2/2: Indexing, please wait..."
                        });
                    }

                    return true;
                }
            }
        };

        var signature = new Signature(
            new Identity("USERNAME", "PASSWORD"), DateTimeOffset.Now);

        Commands.Pull(repo, signature, options);
    }

    public async Task PopulateMods(string gitCacheFolder, IProgress<ModFetchProgress>? progress)
    {
        modList.Mods = [];

        var totalCount = Directory.EnumerateFiles(gitCacheFolder, "*.ckan", SearchOption.AllDirectories).Count();
        var counter = 0;

        foreach (var modDir in Directory.EnumerateDirectories(gitCacheFolder))
        {
            var dirName = Path.GetFileName(modDir);

            if (dirName.StartsWith('.'))
                continue;

            var ckanFiles = Directory.EnumerateFiles(modDir, "*.ckan").ToList();

            if (ckanFiles.Count == 0)
                continue;

            var modVersions = new List<ModVersion>();

            foreach (var ckanFile in ckanFiles)
            {
                counter++;
                var json = await File.ReadAllTextAsync(ckanFile);
                var version = JsonHelper.ParseVersion(ckanFile, json);
                if (version is null) continue;
                modVersions.Add(version);
                progress?.Report(new ModFetchProgress
                {
                    TotalMods = totalCount,
                    ModsReceived = counter,
                    CurrentModName = "Converting CKAN meta into something useful..."
                });
            }

            if (modVersions.Count == 0)
                continue;

            modVersions.Sort((x, y) =>
            {
                if (x.ReleaseDate.HasValue && y.ReleaseDate.HasValue)
                    return DateTime.Compare(y.ReleaseDate.Value, x.ReleaseDate.Value);

                if (x.ReleaseDate.HasValue) return -1;
                if (y.ReleaseDate.HasValue) return 1;

                return Helpers.CompareVersions(x.Version, y.Version);
            });

            var latestJson = await File.ReadAllTextAsync(
                Path.Combine(modDir, modVersions[0].Id + ".ckan"));
            var mod = JsonHelper.ParseMod(latestJson, modVersions);
            if (mod is not null)
                modList.Mods.Add(mod);
        }
    }
}