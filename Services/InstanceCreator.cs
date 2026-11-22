using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KMRLauncherMvvm.Models;

namespace KMRLauncherMvvm.Services;

public enum CreateStep
{
    Check,
    Files,
    Prefix,
    Save,
    Done
}

public record NewInstanceRequest(string Name, KspZip Zip, string Folder, List<string> Compatible, bool IsPrimary);

public enum ImportMode
{
    Copy,
    InPlace
}

public record ImportRequest(string Name, KspFolderScan Scan, ImportMode Mode, string Folder, bool IsPrimary);

public record CreateProgress(CreateStep Step, string? Detail = null, ExtractionProgress? Unpack = null);

public class InstanceCreator(ZipService zips, Action<AppSettings, Instance> save)
{
    public async Task<Instance> CreateFromZipAsync(AppSettings settings, NewInstanceRequest request,
        IProgress<CreateProgress>? progress = null, CancellationToken cancel = default)
    {
        var folder = Path.GetFullPath(request.Folder);
        if (settings.Instances.Any(i => WinePrefixes.SamePath(i.RootPath, folder)))
            throw new IOException("Another instance already uses this folder.");
        if (File.Exists(folder) || (Directory.Exists(folder) && Directory.EnumerateFileSystemEntries(folder).Any()))
            throw new IOException($"A folder named {Path.GetFileName(folder)} already exists there and isn't empty.");

        progress?.Report(new CreateProgress(CreateStep.Check));
        var (_, buildId) = await Task.Run(() => ZipService.ReadBuild(request.Zip.Path), cancel);
        var version = KspVersions.GetKspVersionFromBuildId(buildId);
        if (version is null)
            throw new InvalidDataException($"Couldn't read a known KSP version from {Path.GetFileName(request.Zip.Path)}.");
        if (version != request.Zip.Version)
            throw new InvalidDataException($"The zip now holds KSP {version}, not {request.Zip.Version}. Add it again in Settings.");
        progress?.Report(new CreateProgress(CreateStep.Files, $"build {buildId} · {version}"));

        var created = !Directory.Exists(folder);
        try
        {
            Directory.CreateDirectory(folder);
            var unpack = new Forward<ExtractionProgress>(p =>
                progress?.Report(new CreateProgress(CreateStep.Files, Unpack: p)));
            await Task.Run(() => zips.ExtractFolderFromZip(request.Zip.Path, request.Zip.RelativeRootPath, folder,
                unpack, cancel), cancel);
            cancel.ThrowIfCancellationRequested();

            progress?.Report(new CreateProgress(CreateStep.Prefix));
            var instance = new Instance(request.Name.Trim(), folder, request.Zip.Version)
            {
                CompatibleKspVersions = [.. request.Compatible]
            };
            return Finish(settings, instance, request.IsPrimary, progress);
        }
        catch
        {
            CleanUp(folder, created);
            throw;
        }
    }

    public async Task<Instance> ImportAsync(AppSettings settings, ImportRequest request,
        IProgress<CreateProgress>? progress = null, CancellationToken cancel = default)
    {
        var scan = request.Scan;
        if (!scan.CanAdd || scan.Version is null) throw new IOException(scan.Error ?? "This isn't a KSP folder.");

        var source = scan.Folder;
        var folder = request.Mode == ImportMode.InPlace ? source : Path.GetFullPath(request.Folder);
        if (settings.Instances.Any(i => WinePrefixes.SamePath(i.RootPath, folder) || WinePrefixes.SamePath(i.RootPath, source)))
            throw new IOException("This folder is already an instance.");

        progress?.Report(new CreateProgress(CreateStep.Check));
        var created = false;
        if (request.Mode == ImportMode.Copy)
        {
            if ((folder + Path.DirectorySeparatorChar).StartsWith(source + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                throw new IOException("The copy can't go inside the folder it copies.");
            if (File.Exists(folder) || (Directory.Exists(folder) && Directory.EnumerateFileSystemEntries(folder).Any()))
                throw new IOException($"A folder named {Path.GetFileName(folder)} already exists there and isn't empty.");
            created = !Directory.Exists(folder);
        }

        try
        {
            progress?.Report(new CreateProgress(CreateStep.Files, $"KSP {scan.Version} · build {scan.BuildId}"));
            if (request.Mode == ImportMode.Copy)
            {
                var copy = new Forward<ExtractionProgress>(p =>
                    progress?.Report(new CreateProgress(CreateStep.Files, Unpack: p)));
                await Task.Run(() => CopyFolder(source, folder, copy, cancel), cancel);
            }

            cancel.ThrowIfCancellationRequested();
            progress?.Report(new CreateProgress(CreateStep.Prefix));

            Instance instance;
            if (scan.KmrData is not null)
            {
                instance = InstanceStore.Load(folder);
                if (instance.IsMissing) throw new IOException(instance.LoadError);
                instance.Name = request.Name.Trim();
                if (request.Mode == ImportMode.Copy) instance.PrefixPath = "";
            }
            else
            {
                instance = new Instance(request.Name.Trim(), folder, scan.Version)
                {
                    CompatibleKspVersions = scan.CkanCompatible ?? KspVersions.DefaultCompatibleVersions(scan.Version),
                    Mods = [.. scan.Mods.Select(m => m.Version)],
                    ModRecords = scan.Mods.ToDictionary(m => m.Version.Identifier, m => m.Record)
                };
            }

            return Finish(settings, instance, request.IsPrimary, progress);
        }
        catch
        {
            if (request.Mode == ImportMode.Copy) CleanUp(folder, created);
            throw;
        }
    }

    private Instance Finish(AppSettings settings, Instance instance, bool isPrimary, IProgress<CreateProgress>? progress)
    {
        progress?.Report(new CreateProgress(CreateStep.Save));
        Add(settings, instance, isPrimary);
        progress?.Report(new CreateProgress(CreateStep.Done));
        return instance;
    }

    private void Add(AppSettings settings, Instance instance, bool isPrimary)
    {
        var previousPrimary = settings.Instances.FirstOrDefault(i => i.IsPrimary);
        var previousParent = settings.InstanceParentFolder;
        instance.IsPrimary = isPrimary;
        if (isPrimary && previousPrimary is not null) previousPrimary.IsPrimary = false;
        settings.InstanceParentFolder = Path.GetDirectoryName(instance.RootPath);
        settings.Instances.Add(instance);
        try
        {
            save(settings, instance);
        }
        catch
        {
            settings.Instances.Remove(instance);
            settings.InstanceParentFolder = previousParent;
            if (previousPrimary is not null) previousPrimary.IsPrimary = true;
            throw;
        }
    }

    private static void CopyFolder(string source, string target, IProgress<ExtractionProgress> progress,
        CancellationToken cancel)
    {
        var staging = Path.Combine(source, InstanceStore.FolderName, "staging") + Path.DirectorySeparatorChar;
        var files = Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories)
            .Where(f => !f.StartsWith(staging, StringComparison.Ordinal))
            .Select(f => new FileInfo(f))
            .ToList();
        var state = new ExtractionProgress { FilesTotal = files.Count, BytesTotal = files.Sum(f => f.Length) };

        Directory.CreateDirectory(target);
        foreach (var file in files)
        {
            cancel.ThrowIfCancellationRequested();
            var relative = Path.GetRelativePath(source, file.FullName);
            var destination = Path.Combine(target, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            file.CopyTo(destination, overwrite: false);

            state.FilesDone++;
            state.BytesWritten += file.Length;
            progress.Report(new ExtractionProgress
            {
                Percentage = state.FilesTotal == 0 ? 1 : (double)state.FilesDone / state.FilesTotal,
                CurrentFile = relative,
                FilesDone = state.FilesDone,
                FilesTotal = state.FilesTotal,
                BytesWritten = state.BytesWritten,
                BytesTotal = state.BytesTotal
            });
        }
    }

    private static void CleanUp(string folder, bool created)
    {
        try
        {
            if (!Directory.Exists(folder)) return;
            if (created)
            {
                Directory.Delete(folder, recursive: true);
                return;
            }

            foreach (var dir in Directory.EnumerateDirectories(folder)) Directory.Delete(dir, recursive: true);
            foreach (var file in Directory.EnumerateFiles(folder)) File.Delete(file);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }

    private sealed class Forward<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}
