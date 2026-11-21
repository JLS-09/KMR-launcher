using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using KMRLauncherMvvm.Exceptions;
using KMRLauncherMvvm.Models;

namespace KMRLauncherMvvm.Services;

public record InstallStep(ZipArchiveEntry Entry, string TargetPath, bool MayCreateDirectories);

public static class ModInstaller
{
    private static readonly string[] ReservedPaths = ["GameData", "Ships", "Missions"];

    public static readonly string[] AllowedTargets =
    [
        "GameData", "Missions", "Ships", "Ships/SPH", "Ships/VAB", "Ships/@thumbs/VAB", "Ships/@thumbs/SPH",
        "Ships/Script", "Tutorial", "Scenarios", "GameRoot"
    ];

    public static List<InstallStep> BuildPlan(ModVersion mod, ZipArchive archive)
    {
        var files = new Dictionary<string, ZipArchiveEntry>();
        var directories = new HashSet<string>();

        foreach (var entry in archive.Entries)
        {
            var path = Normalize(entry.FullName);
            if (path.Length == 0) continue;

            var isDirectory = entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\');
            if (isDirectory) directories.Add(path);
            else files.TryAdd(path, entry);

            var parent = ParentOf(path);
            while (parent.Length > 0)
            {
                directories.Add(parent);
                parent = ParentOf(parent);
            }
        }

        var directives = mod.Install is { Count: > 0 }
            ? mod.Install
            : [new InstallDirective { Find = mod.Identifier, InstallTo = "GameData" }];

        var steps = new List<InstallStep>();
        var byTarget = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var directive in directives)
        {
            var target = CheckInstallTo(mod, directive.InstallTo);
            var roots = FindRoots(mod, directive, files, directories);
            var added = 0;

            foreach (var (path, entry) in files)
            {
                var root = roots.FirstOrDefault(r => path == r || path.StartsWith(r + "/"));
                if (root is null || !IsWanted(path, directive)) continue;
                if (path.EndsWith(".ckan", StringComparison.OrdinalIgnoreCase)) continue;

                var parentLength = ParentOf(root).Length;
                var relative = parentLength == 0 ? path : path[(parentLength + 1)..];
                if (directive.As is { } newName)
                {
                    if (newName.Length == 0 || newName.Contains('/') || newName.Contains('\\') || newName == "..")
                        throw new ModInstallException($"{Name(mod)} has an invalid \"as\" value: \"{newName}\".");
                    var slash = relative.IndexOf('/');
                    relative = slash < 0 ? newName : newName + relative[slash..];
                }
                else if (target != "GameRoot")
                {
                    var reserved = ReservedPaths.FirstOrDefault(r =>
                        relative.StartsWith(r + "/", StringComparison.OrdinalIgnoreCase));
                    if (reserved is not null) relative = relative[(reserved.Length + 1)..];
                }

                if (relative.Split('/').Any(part => part is ".." or "."))
                    throw new ModInstallException($"{Name(mod)} contains a file with an unsafe path: \"{path}\".");

                var targetPath = target == "GameRoot" ? relative : $"{target}/{relative}";
                if (byTarget.TryGetValue(targetPath, out var other))
                {
                    if (other == path)
                    {
                        added++;
                        continue;
                    }

                    throw new ModInstallException(
                        $"{Name(mod)} would install both \"{other}\" and \"{path}\" to \"{targetPath}\".");
                }

                byTarget[targetPath] = path;
                steps.Add(new InstallStep(entry, targetPath, MayCreateDirectories(target, targetPath)));
                added++;
            }

            if (added == 0)
                throw new ModInstallException(
                    $"{Name(mod)}: the install directive for \"{string.Join("\", \"", roots)}\" doesn't install any files.");
        }

        return steps;
    }

    private static string CheckInstallTo(ModVersion mod, string? installTo)
    {
        var target = Normalize(installTo ?? "");
        var parts = target.Split('/');
        var isSubfolder = parts.Length > 1 && parts[0] == "GameData" &&
                          parts.All(p => p.Length > 0 && p != "." && p != "..");

        if (AllowedTargets.Contains(target) || isSubfolder) return target;
        throw new ModInstallException($"{Name(mod)} wants to install to \"{installTo}\", which isn't allowed.");
    }

    private static List<string> FindRoots(ModVersion mod, InstallDirective directive,
        Dictionary<string, ZipArchiveEntry> files, HashSet<string> directories)
    {
        var sources = new[] { directive.File, directive.Find, directive.FindRegexp }.Count(s => s is not null);
        if (sources != 1)
            throw new ModInstallException(
                $"{Name(mod)} has an install directive without exactly one of \"file\", \"find\" or \"find_regexp\".");

        if (directive.File is { } file)
        {
            var path = Normalize(file);
            var exact = files.Keys.Concat(directories)
                .Where(p => p.Equals(path, StringComparison.OrdinalIgnoreCase))
                .Distinct()
                .ToList();
            if (exact.Count > 0) return exact;
            throw new ModInstallException($"{Name(mod)}: \"{file}\" isn't in the downloaded archive.");
        }

        var candidates = directive.FindMatchesFiles == true
            ? directories.Concat(files.Keys).ToList()
            : directories.ToList();
        var what = directive.FindMatchesFiles == true ? "a folder or file" : "a folder";

        List<string> roots;
        string description;
        if (directive.Find is { } rawFind)
        {
            var find = Normalize(rawFind);
            var matches = candidates
                .Select(path => (Path: path, Start: FindStart(path, find)))
                .Where(m => m.Start >= 0)
                .ToList();
            var earliest = matches.Count > 0 ? matches.Min(m => m.Start) : -1;
            roots = [.. matches.Where(m => m.Start == earliest).Select(m => m.Path)];
            description = find.Contains('/') ? $"{what} at \"{find}\"" : $"{what} named \"{find}\"";
        }
        else
        {
            Regex regex;
            try
            {
                regex = new Regex(directive.FindRegexp!, RegexOptions.IgnoreCase);
            }
            catch (ArgumentException)
            {
                throw new ModInstallException($"{Name(mod)} has an invalid find_regexp: \"{directive.FindRegexp}\".");
            }

            roots = [.. candidates.Where(path => regex.IsMatch(path))];
            description = $"{what} matching \"{directive.FindRegexp}\"";
        }

        roots =
        [
            .. roots.Where(r => !roots.Any(other => r != other && r.StartsWith(other + "/")))
                .Distinct()
                .Order(StringComparer.Ordinal)
        ];

        return roots.Count > 0
            ? roots
            : throw new ModInstallException($"{Name(mod)}: the downloaded archive doesn't contain {description}.");
    }

    private static int FindStart(string path, string find)
    {
        if (path.Equals(find, StringComparison.OrdinalIgnoreCase)) return 0;
        return path.EndsWith("/" + find, StringComparison.OrdinalIgnoreCase) ? path.Length - find.Length - 1 : -1;
    }

    private static bool MayCreateDirectories(string target, string targetPath) =>
        target is "GameData" or "Tutorial" or "Scenarios" or "Missions" || target.StartsWith("GameData/") ||
        targetPath.StartsWith("Ships/Script/");

    private static bool IsWanted(string path, InstallDirective directive)
    {
        var parts = path.Split('/');

        if (directive.Filter is { } filter &&
            filter.Any(f => parts.Contains(f, StringComparer.OrdinalIgnoreCase)))
            return false;

        if (directive.FilterRegexp is { } filterRegexp && filterRegexp.Any(r => Regex.IsMatch(path, r)))
            return false;

        if (directive.IncludeOnly is null && directive.IncludeOnlyRegexp is null) return true;

        return (directive.IncludeOnly?.Any(i => parts.Contains(i, StringComparer.OrdinalIgnoreCase)) ?? false) ||
               (directive.IncludeOnlyRegexp?.Any(r => Regex.IsMatch(path, r)) ?? false);
    }

    private static string Normalize(string path) => path.Replace('\\', '/').Trim('/');

    private static string ParentOf(string path)
    {
        var slash = path.LastIndexOf('/');
        return slash < 0 ? "" : path[..slash];
    }

    private static string Name(ModVersion mod) => $"{mod.Identifier} {mod.Version}";
}