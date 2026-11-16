using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using KMRLauncherMvvm.Exceptions;
using KMRLauncherMvvm.Models;

namespace KMRLauncherMvvm.Services;

public class InstallRun
{
    public required Instance Instance { get; init; }
    public required IReadOnlyList<ModInstallJob> Jobs { get; init; }
    public required IReadOnlyList<ModVersion> ToRemove { get; init; }

    public static InstallRun Create(Instance instance, InstallModsData data)
    {
        var jobs = data.FinalModList.Select(m => new ModInstallJob(m)).ToList();
        var installing = jobs.Select(j => j.Version.Identifier).ToHashSet();
        var removing = data.RequestedRemoveModVersions.Select(m => m.Version.Identifier).ToHashSet();

        return new InstallRun
        {
            Instance = instance,
            Jobs = jobs,
            ToRemove = [.. instance.Mods.Where(m => installing.Contains(m.Identifier) || removing.Contains(m.Identifier))]
        };
    }
}

public class InstallService(IModDownloader downloader, Action saveSettings)
{
    private const string StagingFolder = ".kmr-staging";

    private static readonly Dictionary<string, string> DlcFolders = new()
    {
        ["MakingHistory-DLC"] = "GameData/SquadExpansion/MakingHistory",
        ["BreakingGround-DLC"] = "GameData/SquadExpansion/Serenity"
    };

    public static List<string> CheckBeforeInstall(InstallRun run)
    {
        var errors = new List<string>();
        if (!Directory.Exists(run.Instance.RootPath))
            errors.Add($"The instance folder \"{run.Instance.RootPath}\" doesn't exist.");

        foreach (var job in run.Jobs)
        {
            var mod = job.Version;
            switch (mod.Kind)
            {
                case null or "package":
                    if (mod.Download is null || mod.Download.All(string.IsNullOrWhiteSpace))
                        errors.Add($"{mod.Identifier} {mod.Version} has no download URL.");
                    break;
                case "metapackage":
                    break;
                case "dlc":
                    if (!DlcFolders.TryGetValue(mod.Identifier, out var folder))
                        errors.Add($"{mod.Identifier} is a DLC the launcher doesn't know how to detect.");
                    else if (!Directory.Exists(Path.Combine(run.Instance.RootPath, folder)))
                        errors.Add($"{mod.Identifier} is a paid DLC. Install it through the store you bought " +
                                   $"KSP from; the launcher can't install it.");
                    break;
                default:
                    errors.Add($"{mod.Identifier} has an unknown kind \"{mod.Kind}\".");
                    break;
            }
        }

        return errors;
    }

    public async Task RunAsync(InstallRun run, int maxParallelDownloads, CancellationToken cancellationToken)
    {
        var errors = CheckBeforeInstall(run);
        if (errors.Count > 0) throw new ModInstallException(string.Join("\n", errors));

        var changes = await Task.Run(() => RunCore(run,
            Math.Clamp(maxParallelDownloads, 1, AppSettings.MaxAllowedParallelDownloads), cancellationToken));

        Commit(run);
        saveSettings();
        await Task.Run(() => Cleanup(run.Instance.RootPath, changes));
    }

    private async Task<RunChanges> RunCore(InstallRun run, int maxParallelDownloads,
        CancellationToken cancellationToken)
    {
        var changes = new RunChanges(Path.Combine(run.Instance.RootPath, StagingFolder,
            $"{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid().ToString()[..8]}"));
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Exception? failure = null;

        void Fail(ModInstallJob? job, Exception e)
        {
            if (job is not null)
            {
                job.State = ModInstallState.Failed;
                job.Error = e.Message;
            }

            Interlocked.CompareExchange(ref failure, e, null);
            cancel.Cancel();
        }

        try
        {
            StageRemovals(run, changes);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Fail(null, new ModInstallException($"Couldn't remove the old files: {e.Message}"));
        }

        var channel = Channel.CreateUnbounded<(ModInstallJob Job, string Archive)>();
        var installer = Task.Run(async () =>
        {
            try
            {
                await foreach (var (job, archive) in channel.Reader.ReadAllAsync(cancel.Token))
                {
                    try
                    {
                        Install(run, job, archive, changes, cancel.Token);
                    }
                    catch (OperationCanceledException) when (cancel.IsCancellationRequested)
                    {
                        job.State = ModInstallState.Cancelled;
                    }
                    catch (Exception e)
                    {
                        Fail(job, e is ModInstallException
                            ? e
                            : new ModInstallException($"Installing {job.Version.Identifier} failed: {e.Message}"));
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
        });

        using var slots = new SemaphoreSlim(maxParallelDownloads);
        var downloads = run.Jobs.Select(async job =>
        {
            if (failure is not null) return;
            if (!job.NeedsDownload)
            {
                job.State = ModInstallState.Done;
                return;
            }

            try
            {
                var archive = downloader.FindCached(job.Version);
                if (archive is not null)
                {
                    job.IsCached = true;
                    job.BytesTotal = job.BytesDone = new FileInfo(archive).Length;
                }
                else
                {
                    await slots.WaitAsync(cancel.Token);
                    try
                    {
                        job.State = ModInstallState.Downloading;
                        archive = await downloader.DownloadAsync(job.Version, (done, total) =>
                        {
                            Interlocked.Exchange(ref job.BytesDone, done);
                            job.BytesTotal = total;
                        }, cancel.Token);
                    }
                    finally
                    {
                        slots.Release();
                    }
                }

                job.State = ModInstallState.Downloaded;
                await channel.Writer.WriteAsync((job, archive), cancel.Token);
            }
            catch (OperationCanceledException) when (cancel.IsCancellationRequested)
            {
                job.State = ModInstallState.Cancelled;
            }
            catch (Exception e)
            {
                Fail(job, e is ModInstallException
                    ? e
                    : new ModInstallException($"Downloading {job.Version.Identifier} failed: {e.Message}"));
            }
        }).ToList();

        await Task.WhenAll(downloads);
        channel.Writer.Complete();
        await installer;

        foreach (var job in run.Jobs.Where(j => j.State is ModInstallState.Queued or ModInstallState.Downloaded))
            job.State = ModInstallState.Cancelled;

        if (failure is null && !cancellationToken.IsCancellationRequested) return changes;

        var rollbackProblems = Rollback(run.Instance.RootPath, changes);
        var message = failure?.Message ?? "The install was cancelled.";
        if (rollbackProblems.Count > 0)
            message += "\n\nSome changes couldn't be undone:\n" + string.Join("\n", rollbackProblems);

        if (failure is null) throw new OperationCanceledException(message, cancellationToken);
        throw new ModInstallException(message);
    }

    private static void StageRemovals(InstallRun run, RunChanges changes)
    {
        foreach (var mod in run.ToRemove)
        {
            if (!run.Instance.ModRecords.TryGetValue(mod.Identifier, out var record)) continue;

            foreach (var file in record.Files)
            {
                var source = FullPath(run.Instance.RootPath, file);
                if (!File.Exists(source)) continue;

                var staged = Path.Combine(changes.StagingPath, file);
                Directory.CreateDirectory(Path.GetDirectoryName(staged)!);
                File.Move(source, staged);
                changes.Staged.Add(file);
            }
        }
    }

    private static void Install(InstallRun run, ModInstallJob job, string archivePath, RunChanges changes,
        CancellationToken cancellationToken)
    {
        job.State = ModInstallState.Installing;
        var root = run.Instance.RootPath;
        var name = $"{job.Version.Identifier} {job.Version.Version}";

        ZipArchive zip;
        try
        {
            zip = ZipFile.OpenRead(archivePath);
        }
        catch (InvalidDataException)
        {
            throw new ModInstallException($"The download of {name} isn't a zip file.");
        }

        using (zip)
        {
            var steps = ModInstaller.BuildPlan(job.Version, zip);
            job.FilesTotal = steps.Count;

            // Check every target before writing anything
            foreach (var step in steps)
            {
                var target = FullPath(root, step.TargetPath);
                if (File.Exists(target) || Directory.Exists(target))
                    throw new ModInstallException(
                        $"{name} wants to install \"{step.TargetPath}\", but it already exists" +
                        $"{Owner(run, step.TargetPath)}. Remove it first, then try again.");

                var folder = Path.GetDirectoryName(target)!;
                if (!step.MayCreateDirectories && !Directory.Exists(folder))
                    throw new ModInstallException(
                        $"{name} wants to install into \"{Path.GetRelativePath(root, folder)}\", which doesn't exist " +
                        $"in this instance.");
            }

            foreach (var step in steps)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var target = FullPath(root, step.TargetPath);
                CreateFolders(root, Path.GetDirectoryName(target)!, changes);

                // Recorded before writing, so a half-written file is removed by a rollback too
                lock (changes) changes.Written.Add(step.TargetPath);
                job.InstalledFiles.Add(step.TargetPath);
                step.Entry.ExtractToFile(target, overwrite: false);
                job.FilesDone++;
            }
        }

        job.State = ModInstallState.Done;
    }

    private static void CreateFolders(string root, string folder, RunChanges changes)
    {
        var missing = new Stack<string>();
        for (var current = folder; !Directory.Exists(current); current = Path.GetDirectoryName(current)!)
            missing.Push(current);

        foreach (var path in missing)
        {
            Directory.CreateDirectory(path);
            lock (changes) changes.CreatedFolders.Add(Path.GetRelativePath(root, path));
        }
    }

    private static void Commit(InstallRun run)
    {
        var instance = run.Instance;
        foreach (var removed in run.ToRemove)
        {
            instance.Mods.RemoveAll(m => m.Identifier == removed.Identifier);
            instance.ModRecords.Remove(removed.Identifier);
        }

        foreach (var job in run.Jobs)
        {
            instance.Mods.RemoveAll(m => m.Identifier == job.Version.Identifier);
            instance.Mods.Add(job.Version);
            instance.ModRecords[job.Version.Identifier] = new InstalledModRecord
            {
                Files = [.. job.InstalledFiles],
                IsUserRequested = job.Planned.IsUserRequested
            };
        }
    }

    private static void Cleanup(string root, RunChanges changes)
    {
        TryDeleteFolder(changes.StagingPath);
        TryDeleteEmptyFolder(Path.Combine(root, StagingFolder));

        var folders = changes.Staged
            .Select(file => Path.GetDirectoryName(file.Replace('/', Path.DirectorySeparatorChar)))
            .Where(f => !string.IsNullOrEmpty(f))
            .Distinct()
            .OrderByDescending(f => f!.Length);
        foreach (var folder in folders)
        {
            for (var current = folder; !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
            {
                if (IsGameFolder(current) || !TryDeleteEmptyFolder(Path.Combine(root, current))) break;
            }
        }
    }

    private static List<string> Rollback(string root, RunChanges changes)
    {
        var problems = new List<string>();

        foreach (var file in Enumerable.Reverse(changes.Written))
        {
            try
            {
                File.Delete(FullPath(root, file));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                problems.Add($"Couldn't delete \"{file}\": {e.Message}");
            }
        }

        foreach (var folder in Enumerable.Reverse(changes.CreatedFolders))
            TryDeleteEmptyFolder(Path.Combine(root, folder));

        foreach (var file in changes.Staged)
        {
            try
            {
                var target = FullPath(root, file);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Move(Path.Combine(changes.StagingPath, file), target);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                problems.Add($"Couldn't restore \"{file}\" (it's still in {changes.StagingPath}): {e.Message}");
            }
        }

        if (problems.Count == 0)
        {
            TryDeleteFolder(changes.StagingPath);
            TryDeleteEmptyFolder(Path.Combine(root, StagingFolder));
        }

        return problems;
    }

    private static string Owner(InstallRun run, string path)
    {
        var owner = run.Instance.ModRecords.FirstOrDefault(r => r.Value.Files.Contains(path)).Key;
        return owner is null ? " and no mod installed it" : $" (installed by {owner})";
    }

    private static bool IsGameFolder(string relativeFolder) =>
        ModInstaller.AllowedTargets.Contains(relativeFolder.Replace(Path.DirectorySeparatorChar, '/'));

    private static string FullPath(string root, string relativePath)
    {
        var fullRoot = Path.GetFullPath(root);
        var full = Path.GetFullPath(Path.Combine(fullRoot, relativePath));
        if (!full.StartsWith(fullRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar))
            throw new ModInstallException($"\"{relativePath}\" is outside the instance folder.");
        return full;
    }

    private static bool TryDeleteEmptyFolder(string path)
    {
        try
        {
            if (!Directory.Exists(path) || Directory.EnumerateFileSystemEntries(path).Any()) return false;
            Directory.Delete(path);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static void TryDeleteFolder(string path)
    {
        try
        {
            if (Directory.Exists(path)) Directory.Delete(path, true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }

    private class RunChanges(string stagingPath)
    {
        public string StagingPath { get; } = stagingPath;
        public List<string> Staged { get; } = [];
        public List<string> Written { get; } = [];
        public List<string> CreatedFolders { get; } = [];
    }
}