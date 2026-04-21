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
    
    public static string ToTimeAgo(this DateTime pastDate, DateTime? referenceDate = null)
    {
        DateTime now = referenceDate ?? DateTime.UtcNow;

        if (pastDate > now)
            return "in the future";

        int years = now.Year - pastDate.Year;
        DateTime yearAdjusted = pastDate.AddYears(years);

        if (yearAdjusted > now)
        {
            years--;
            yearAdjusted = pastDate.AddYears(years);
        }

        if (years > 0)
            return years == 1 ? "1 year ago" : $"{years} years ago";

        var months = (now.Year - pastDate.Year) * 12 + now.Month - pastDate.Month;
        var monthAdjusted = pastDate.AddMonths(months);

        if (monthAdjusted > now)
        {
            months--;
            monthAdjusted = pastDate.AddMonths(months);
        }

        if (months > 0)
            return months == 1 ? "1 month ago" : $"{months} months ago";

        var diff = now - pastDate;

        if (diff.Days > 0)
            return diff.Days == 1 ? "1 day ago" : $"{diff.Days} days ago";

        if (diff.Hours > 0)
            return diff.Hours == 1 ? "1 hour ago" : $"{diff.Hours} hours ago";

        if (diff.Minutes > 0)
            return diff.Minutes == 1 ? "1 minute ago" : $"{diff.Minutes} minutes ago";

        return diff.Seconds <= 1 ? "just now" : $"{diff.Seconds} seconds ago";
    
    }

    private static string Plural(double value) => value >= 2 ? "s" : "";
}