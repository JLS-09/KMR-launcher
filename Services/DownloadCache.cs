using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KMRLauncherMvvm.Models;

namespace KMRLauncherMvvm.Services;

public record CacheUsage(long InUseBytes, long UnusedBytes, int ArchiveCount, int UnusedCount);

public record CacheCleanup(int Removed, long FreedBytes, int Failed);

public class DownloadCache(string folder, Func<ModVersion, string> cachePath)
{
    public string Folder => folder;

    public CacheUsage Measure(IEnumerable<Instance> instances)
    {
        var (used, unused) = Split(instances);
        return new CacheUsage(used.Sum(f => f.Length), unused.Sum(f => f.Length), used.Count + unused.Count,
            unused.Count);
    }

    public CacheCleanup RemoveUnused(IEnumerable<Instance> instances) => Delete(Split(instances).Unused);

    public CacheCleanup ClearAll() => Delete(Files());

    private (List<FileInfo> Used, List<FileInfo> Unused) Split(IEnumerable<Instance> instances)
    {
        var needed = instances
            .SelectMany(i => i.Mods)
            .Select(m => Path.GetFullPath(cachePath(m)))
            .ToHashSet(StringComparer.Ordinal);

        var used = new List<FileInfo>();
        var unused = new List<FileInfo>();
        foreach (var file in Files()) (needed.Contains(file.FullName) ? used : unused).Add(file);
        return (used, unused);
    }

    private List<FileInfo> Files()
    {
        var directory = new DirectoryInfo(folder);
        return directory.Exists ? [.. directory.EnumerateFiles()] : [];
    }

    private static CacheCleanup Delete(List<FileInfo> files)
    {
        var removed = 0;
        var failed = 0;
        long freed = 0;
        foreach (var file in files)
        {
            try
            {
                var length = file.Length;
                file.Delete();
                removed++;
                freed += length;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                failed++;
            }
        }

        return new CacheCleanup(removed, freed, failed);
    }
}
