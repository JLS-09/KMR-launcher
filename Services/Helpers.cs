using System;
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
}