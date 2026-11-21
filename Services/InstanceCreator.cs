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
    CheckZip,
    Unpack,
    Prefix,
    Save,
    Done
}

public record NewInstanceRequest(string Name, KspZip Zip, string Folder, List<string> Compatible, bool IsPrimary);

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

        progress?.Report(new CreateProgress(CreateStep.CheckZip));
        var (_, buildId) = await Task.Run(() => ZipService.ReadBuild(request.Zip.Path), cancel);
        var version = KspVersions.GetKspVersionFromBuildId(buildId);
        if (version is null)
            throw new InvalidDataException($"Couldn't read a known KSP version from {Path.GetFileName(request.Zip.Path)}.");
        if (version != request.Zip.Version)
            throw new InvalidDataException($"The zip now holds KSP {version}, not {request.Zip.Version}. Add it again in Settings.");
        progress?.Report(new CreateProgress(CreateStep.Unpack, $"build {buildId} · {version}"));

        var created = !Directory.Exists(folder);
        try
        {
            Directory.CreateDirectory(folder);
            var unpack = new Forward<ExtractionProgress>(p =>
                progress?.Report(new CreateProgress(CreateStep.Unpack, Unpack: p)));
            await Task.Run(() => zips.ExtractFolderFromZip(request.Zip.Path, request.Zip.RelativeRootPath, folder,
                unpack, cancel), cancel);
            cancel.ThrowIfCancellationRequested();

            progress?.Report(new CreateProgress(CreateStep.Prefix));
            var instance = new Instance(request.Name.Trim(), folder, request.Zip.Version)
            {
                CompatibleKspVersions = [.. request.Compatible],
                IsPrimary = request.IsPrimary
            };
            var previousPrimary = settings.Instances.FirstOrDefault(i => i.IsPrimary);
            if (request.IsPrimary && previousPrimary is not null) previousPrimary.IsPrimary = false;

            progress?.Report(new CreateProgress(CreateStep.Save));
            var previousParent = settings.InstanceParentFolder;
            settings.InstanceParentFolder = Path.GetDirectoryName(folder);
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

            progress?.Report(new CreateProgress(CreateStep.Done));
            return instance;
        }
        catch
        {
            CleanUp(folder, created);
            throw;
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
