using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace KMRLauncherMvvm.Services;

public static class Helpers
{
    public static long GetDirectorySize(string folderPath)
    {
        if (!Directory.Exists(folderPath))
            throw new DirectoryNotFoundException($"Directory not found: {folderPath}");

        long totalSize = 0;

        try
        {
            foreach (var file in Directory.GetFiles(folderPath))
            {
                try
                {
                    totalSize += new FileInfo(file).Length;
                }
                catch (UnauthorizedAccessException) {}
            }

            totalSize += Directory.GetDirectories(folderPath).Sum(GetDirectorySize);
        }
        catch (UnauthorizedAccessException) {}

        return totalSize;
    }
    
    public static string BytesToString(long byteCount)
    {
        string[] suf = ["B", "KB", "MB", "GB", "TB", "PB", "EB"];
        if (byteCount == 0)
            return "0" + suf[0];
        var bytes = Math.Abs(byteCount);
        var place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
        var num = Math.Round(bytes / Math.Pow(1024, place), 2);
        return Math.Sign(byteCount) * num + " " + suf[place];
    }
    
    public static int CompareVersions(string a, string b)
    {
        var (epochA, numA, suffixA) = ParseVersion(a);
        var (epochB, numB, suffixB) = ParseVersion(b);

        var epochCmp = epochB.CompareTo(epochA);
        if (epochCmp != 0) return epochCmp;

        var len = Math.Max(numA.Count, numB.Count);
        for (var i = 0; i < len; i++)
        {
            var va = i < numA.Count ? numA[i] : 0;
            var vb = i < numB.Count ? numB[i] : 0;
            if (va != vb) return vb.CompareTo(va);
        }

        if (string.IsNullOrEmpty(suffixA) && !string.IsNullOrEmpty(suffixB)) return 1;
        if (!string.IsNullOrEmpty(suffixA) && string.IsNullOrEmpty(suffixB)) return -1;
        return string.CompareOrdinal(suffixB, suffixA);
    }

    private static (int epoch, List<int> numbers, string suffix) ParseVersion(string version)
    {
        var epoch = 0;
        var colonIdx = version.IndexOf(':');
        if (colonIdx > 0 && int.TryParse(version[..colonIdx], out var parsedEpoch))
        {
            epoch = parsedEpoch;
            version = version[(colonIdx + 1)..];
        }

        version = version.TrimStart('v', 'V');

        var match = System.Text.RegularExpressions.Regex.Match(version, @"^([\d.]+)(.*)$");
        var numericPart = match.Success ? match.Groups[1].Value : "";
        var suffix = match.Success ? match.Groups[2].Value : version;

        var numbers = numericPart
            .Split('.')
            .Where(s => s.Length > 0)
            .Select(s => int.TryParse(s, out var n) ? n : 0)
            .ToList();

        return (epoch, numbers, suffix);
    }
}