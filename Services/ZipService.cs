using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KMRLauncherMvvm.Models;

namespace KMRLauncherMvvm.Services;

public class ZipService
{
    private const string TargetFileName = "buildID64.txt";
    private const string SearchTerm = "build id = ";

    public KspZip GetVersionFromKspZip(string kspZipPath)
    {
        var (root, buildId) = ReadBuild(kspZipPath);
        return new KspZip(kspZipPath, root, KspVersions.GetKspVersionFromBuildId(buildId) ?? "");
    }

    public static (string Root, string BuildId) ReadBuild(string kspZipPath)
    {
        try
        {
            using var archive = ZipFile.OpenRead(kspZipPath);

            var entry = archive.Entries
                .FirstOrDefault(e => Path.GetFileName(e.FullName)
                    .Equals(TargetFileName, StringComparison.OrdinalIgnoreCase));
            if (entry is null) return ("", "");

            using var reader = new StreamReader(entry.Open());
            return (Path.GetDirectoryName(entry.FullName) ?? "", ReadBuildId(reader));
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            return ("", "");
        }
    }

    public static string ReadBuildId(TextReader reader)
    {
        while (reader.ReadLine() is { } line)
        {
            var at = line.IndexOf(SearchTerm, StringComparison.OrdinalIgnoreCase);
            if (at >= 0) return line[(at + SearchTerm.Length)..].Trim();
        }

        return "";
    }

    public (KspZip? Zip, string? Error) AddToSettings(AppSettings settings, string path)
    {
        var name = Path.GetFileName(path);
        if (settings.KspZips.Any(z => z.Path == path)) return (null, $"{name} is already in the list.");

        var zip = GetVersionFromKspZip(path);
        if (string.IsNullOrEmpty(zip.Version))
            return (null, $"Couldn't read a known KSP version from {name}. Is it a KSP zip?");

        settings.KspZips.Add(zip);
        return (zip, null);
    }

    public static (int Files, long Bytes) Measure(KspZip zip)
    {
        using var archive = ZipFile.OpenRead(zip.Path);
        var files = Entries(archive, zip.RelativeRootPath).Where(e => !e.RelativePath.EndsWith('/')).ToList();
        return (files.Count, files.Sum(e => e.Entry.Length));
    }

    public async Task ExtractFolderFromZip(string zipPath, string folderInZip, string destinationPath,
        IProgress<ExtractionProgress>? progress = null, CancellationToken cancel = default)
    {
        await using var archive = await ZipFile.OpenReadAsync(zipPath, cancel);

        var entries = Entries(archive, folderInZip).ToList();
        var files = entries.Where(e => !e.RelativePath.EndsWith('/')).ToList();
        var state = new ExtractionProgress { FilesTotal = files.Count, BytesTotal = files.Sum(e => e.Entry.Length) };
        var root = Path.GetFullPath(destinationPath) + Path.DirectorySeparatorChar;

        foreach (var (entry, relativePath) in entries)
        {
            cancel.ThrowIfCancellationRequested();

            var destinationFilePath = Path.GetFullPath(Path.Combine(destinationPath, relativePath));
            if (!destinationFilePath.StartsWith(root, StringComparison.Ordinal))
                throw new InvalidDataException($"The zip has a path outside its folder: {entry.FullName}");

            if (relativePath.EndsWith('/'))
            {
                Directory.CreateDirectory(destinationFilePath);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destinationFilePath)!);
            await entry.ExtractToFileAsync(destinationFilePath, overwrite: false, cancel);

            state.FilesDone++;
            state.BytesWritten += entry.Length;
            progress?.Report(new ExtractionProgress
            {
                Percentage = state.FilesTotal == 0 ? 1 : (double)state.FilesDone / state.FilesTotal,
                CurrentFile = relativePath,
                FilesDone = state.FilesDone,
                FilesTotal = state.FilesTotal,
                BytesWritten = state.BytesWritten,
                BytesTotal = state.BytesTotal
            });
        }
    }

    private static IEnumerable<(ZipArchiveEntry Entry, string RelativePath)> Entries(ZipArchive archive,
        string folderInZip)
    {
        var prefix = folderInZip.Replace('\\', '/').Trim('/');
        if (prefix.Length > 0) prefix += "/";

        foreach (var entry in archive.Entries)
        {
            var name = entry.FullName.Replace('\\', '/');
            if (!name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;

            var relativePath = name[prefix.Length..];
            if (relativePath.Length > 0) yield return (entry, relativePath);
        }
    }
}
