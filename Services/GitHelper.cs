using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using KMRLauncherMvvm.Models;
using LibGit2Sharp;

namespace KMRLauncherMvvm.Services;

public class GitHelper(ModListService modList)
{
    public const string CkanMetaUrl = "https://github.com/KSP-CKAN/CKAN-meta.git";
    public const string CkanMetaBranch = "master";

    public static string DefaultCkanMetaFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "kmrLauncher/cache/ckan-meta");

    public static string? ReadHeadSha(string folder)
    {
        try
        {
            if (!Repository.IsValid(folder)) return null;

            using var repo = new Repository(folder);
            var branch = repo.Branches[CkanMetaBranch] ?? repo.Head;
            return branch.Tip?.Sha;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static void CloneCkanMetaRepo(string gitCacheFolder, IProgress<ModFetchProgress>? progress,
        CancellationToken cancel = default)
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
                    CurrentModName = $"3/3: Checkout {path}",
                    Phase = FetchPhase.CheckingOut
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
                            ReceivedBytes = cloneProgress.ReceivedBytes,
                            Phase = FetchPhase.Receiving,
                            CurrentModName = "1/3: Cloning CKAN repo"
                        });
                    }
                    else
                    {
                        progress?.Report(new ModFetchProgress
                        {
                            TotalMods = cloneProgress.TotalObjects,
                            ModsReceived = cloneProgress.IndexedObjects,
                            ReceivedBytes = cloneProgress.ReceivedBytes,
                            Phase = FetchPhase.Indexing,
                            CurrentModName = "2/3: Indexing, please wait..."
                        });
                    }

                    return !cancel.IsCancellationRequested;
                }
            }
        };

        Repository.Clone(CkanMetaUrl, gitCacheFolder, cloneOptions);
    }

    public static void PullCkanMetaRepo(string gitCacheFolder, IProgress<ModFetchProgress>? progress,
        CancellationToken cancel = default)
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
                            ReceivedBytes = transferProgress.ReceivedBytes,
                            Phase = FetchPhase.Receiving,
                            CurrentModName = "1/2: Cloning CKAN repo"
                        });
                    }
                    else
                    {
                        progress?.Report(new ModFetchProgress
                        {
                            TotalMods = transferProgress.TotalObjects,
                            ModsReceived = transferProgress.IndexedObjects,
                            ReceivedBytes = transferProgress.ReceivedBytes,
                            Phase = FetchPhase.Indexing,
                            CurrentModName = "2/2: Indexing, please wait..."
                        });
                    }

                    return !cancel.IsCancellationRequested;
                }
            }
        };

        var signature = new Signature(
            new Identity("USERNAME", "PASSWORD"), DateTimeOffset.Now);

        Commands.Pull(repo, signature, options);
    }

    public static Dictionary<string, long> ReadDownloadCounts(string gitCacheFolder)
    {
        try
        {
            var json = File.ReadAllText(Path.Combine(gitCacheFolder, "download_counts.json"));
            return JsonSerializer.Deserialize<Dictionary<string, long>>(json) ?? [];
        }
        catch (Exception)
        {
            return [];
        }
    }

    public async Task PopulateMods(string gitCacheFolder, IProgress<ModFetchProgress>? progress)
    {
        ObservableCollection<Mod> mods = [];

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

            modVersions.Sort((x, y) => CkanVersion.Compare(y.Version, x.Version));

            var latestJson = await File.ReadAllTextAsync(
                Path.Combine(modDir, modVersions[0].Id + ".ckan"));
            var mod = JsonHelper.ParseMod(latestJson, modVersions);
            if (mod is not null)
                mods.Add(mod);
        }

        modList.Mods = mods;
    }
}