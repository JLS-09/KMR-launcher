using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KMRLauncherMvvm.Models;

namespace KMRLauncherMvvm.Services;

public static class WinePrefixes
{
    private static readonly string ConfigDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

    public static readonly string Root = Path.Combine(ConfigDir, "kmrLauncher", "prefixes");

    public static void AssignMissing(IList<Instance> instances, string? root = null)
    {
        foreach (var instance in instances.Where(i => string.IsNullOrWhiteSpace(i.PrefixPath)))
            instance.PrefixPath = DefaultFor(instance, instances, root ?? Root);
    }

    public static string DefaultFor(Instance instance, IEnumerable<Instance> all) => DefaultFor(instance, all, Root);

    public static string DefaultFor(Instance instance, IEnumerable<Instance> all, string root)
    {
        var taken = all.Where(i => i != instance)
            .Select(i => i.PrefixPath)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(Normalize)
            .ToHashSet();

        var folderName = Path.GetFileName(instance.RootPath.TrimEnd('/', '\\'));
        if (folderName.Length == 0) folderName = instance.Name.Trim();
        if (folderName.Length == 0) folderName = "instance";

        var candidate = Path.Combine(root, folderName);
        for (var n = 2; taken.Contains(Normalize(candidate)); n++)
            candidate = Path.Combine(root, $"{folderName}-{n}");

        return candidate;
    }

    public static bool IsManaged(string prefixPath) => IsManaged(prefixPath, Root);

    public static bool IsManaged(string prefixPath, string root) =>
        !string.IsNullOrWhiteSpace(prefixPath) &&
        Path.GetDirectoryName(Normalize(prefixPath)) == Normalize(root);

    public static string Expand(string path)
    {
        path = path.Trim();
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (path == "~") return home;
        return path.StartsWith("~/") ? Path.Combine(home, path[2..]) : path;
    }

    public static bool SamePath(string a, string b) =>
        !string.IsNullOrWhiteSpace(a) && !string.IsNullOrWhiteSpace(b) && Normalize(a) == Normalize(b);

    private static string Normalize(string path) =>
        string.IsNullOrWhiteSpace(path) ? "" : Path.GetFullPath(Expand(path)).TrimEnd('/', '\\');
}