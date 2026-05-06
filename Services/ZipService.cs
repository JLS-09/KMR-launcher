using System;
using System.Data;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using KMRLauncherMvvm.Models;

namespace KMRLauncherMvvm.Services;

public class ZipService
{
    private const string TargetFileName = "buildID64.txt";
    private const string SearchTerm = "build id = ";
    
    public KspZip GetVersionFromKspZip(string kspZipPath)
    {
        try
        {
            using var zipStream = new FileStream(kspZipPath, FileMode.Open, FileAccess.Read);
            using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);
            
            var entry = archive.Entries
                .FirstOrDefault(e => Path.GetFileName(e.FullName)
                    .Equals(TargetFileName, StringComparison.OrdinalIgnoreCase));
            
            var parentFolderPath = Path.GetDirectoryName(entry.FullName) ?? string.Empty;
                
            using var entryStream = entry.Open();
            using var reader = new StreamReader(entryStream);

            
            string? matchedLine = null;

            while (reader.ReadLine() is { } line)
            {
                if (!line.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase)) continue;
                matchedLine = line;
                break;
            }
            
            var buildId = matchedLine?.Replace("build id = ", "");
            
            return new KspZip(kspZipPath, parentFolderPath, KspVersions.GetKspVersionFromBuildId(buildId ?? "") ?? "");
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return new KspZip("", "", "");
        }
    }
    
    public async Task ExtractFolderFromZip(string zipPath, string folderInZip, string destinationPath, IProgress<ExtractionProgress>? progress = null)
    {
        try
        {
            folderInZip = folderInZip.Replace("\\", "/").TrimEnd('/') + "/";

            await using var archive = await ZipFile.OpenReadAsync(zipPath);
            
            var entries = archive.Entries
                .Where(e => e.FullName.StartsWith(folderInZip, StringComparison.OrdinalIgnoreCase))
                .ToList();
            
            var total = entries.Count;
            var current = 0;
            
            foreach (var entry in archive.Entries)
            {
                if (!entry.FullName.StartsWith(folderInZip, StringComparison.OrdinalIgnoreCase))
                    continue;

                var relativePath = entry.FullName[folderInZip.Length..];

                if (string.IsNullOrEmpty(relativePath))
                    continue;

                var destinationFilePath = Path.Combine(destinationPath, relativePath);

                if (entry.FullName.EndsWith('/'))
                {
                    Directory.CreateDirectory(destinationFilePath);
                }
                else
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(destinationFilePath)!);
                    
                    progress?.Report(new ExtractionProgress
                    {
                        Percentage = (double)current / total,
                        CurrentFile = relativePath
                    });
                    
                    await entry.ExtractToFileAsync(destinationFilePath, overwrite: false);
                }
                
                current++;
            }
            
            progress?.Report(new ExtractionProgress
            {
                Percentage = 1,
                CurrentFile = "Done"
            });
        }
        catch (Exception e)
        {
            throw new Exception("Failed to extract folder from zip", e);
        }
    }
}