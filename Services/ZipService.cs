using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
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
}