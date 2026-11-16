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
    private static readonly string[] AllowedTargets =
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
            var mayCreateDirectories = target == "GameData" || target.StartsWith("GameData/") ||
                                       target == "Tutorial" || target == "Scenarios";
            var root = FindRoot(mod, directive, files, directories);
            var parentLength = ParentOf(root).Length;
            var added = 0;

            foreach (var (path, entry) in files)
            {
                if (path != root && !path.StartsWith(root + "/")) continue;
                if (!IsWanted(path, directive)) continue;

                var relative = parentLength == 0 ? path : path[(parentLength + 1)..];
                if (directive.As is { } newName)
                {
                    if (newName.Length == 0 || newName.Contains('/') || newName.Contains('\\') || newName == "..")
                        throw new ModInstallException($"{Name(mod)} has an invalid \"as\" value: \"{newName}\".");
                    var slash = relative.IndexOf('/');
                    relative = slash < 0 ? newName : newName + relative[slash..];
                }

                if (relative.Split('/').Any(part => part is ".." or "."))
                    throw new ModInstallException($"{Name(mod)} contains a file with an unsafe path: \"{path}\".");

                var targetPath = target == "GameRoot" ? relative : $"{target}/{relative}";
                if (byTarget.TryGetValue(targetPath, out var other))
                {
                    if (other == path) continue; // Two directives picked the same file: install it once
                    throw new ModInstallException(
                        $"{Name(mod)} would install both \"{other}\" and \"{path}\" to \"{targetPath}\".");
                }

                byTarget[targetPath] = path;
                steps.Add(new InstallStep(entry, targetPath, mayCreateDirectories));
                added++;
            }

            if (added == 0)
                throw new ModInstallException(
                    $"{Name(mod)}: the install directive for \"{root}\" doesn't install any files.");
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

    private static string FindRoot(ModVersion mod, InstallDirective directive,
        Dictionary<string, ZipArchiveEntry> files, HashSet<string> directories)
    {
        var sources = new[] { directive.File, directive.Find, directive.FindRegexp }.Count(s => s is not null);
        if (sources != 1)
            throw new ModInstallException(
                $"{Name(mod)} has an install directive without exactly one of \"file\", \"find\" or \"find_regexp\".");

        if (directive.File is { } file)
        {
            var path = Normalize(file);
            if (files.ContainsKey(path) || directories.Contains(path)) return path;
            throw new ModInstallException($"{Name(mod)}: \"{file}\" isn't in the downloaded archive.");
        }

        var candidates = directive.FindMatchesFiles == true ? directories.Concat(files.Keys) : directories;

        IEnumerable<string> matches;
        string description;
        if (directive.Find is { } find)
        {
            matches = candidates.Where(path => LastPart(path) == find);
            description = $"a folder named \"{find}\"";
        }
        else
        {
            Regex regex;
            try
            {
                regex = new Regex(directive.FindRegexp!);
            }
            catch (ArgumentException)
            {
                throw new ModInstallException($"{Name(mod)} has an invalid find_regexp: \"{directive.FindRegexp}\".");
            }

            matches = candidates.Where(path => regex.IsMatch(path));
            description = $"a folder matching \"{directive.FindRegexp}\"";
        }

        // "Top-most": the match with the fewest folders above it; the shortest path breaks a tie
        var root = matches
            .OrderBy(path => path.Count(c => c == '/'))
            .ThenBy(path => path.Length)
            .ThenBy(path => path, StringComparer.Ordinal)
            .FirstOrDefault();

        return root ?? throw new ModInstallException(
            $"{Name(mod)}: the downloaded archive doesn't contain {description}.");
    }

    // filter / filter_regexp / include_only / include_only_regexp, matched against the full path in the zip
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

    // Forward slashes, no leading or trailing slash
    private static string Normalize(string path) => path.Replace('\\', '/').Trim('/');

    private static string ParentOf(string path)
    {
        var slash = path.LastIndexOf('/');
        return slash < 0 ? "" : path[..slash];
    }

    private static string LastPart(string path) => path[(path.LastIndexOf('/') + 1)..];

    private static string Name(ModVersion mod) => $"{mod.Identifier} {mod.Version}";
}