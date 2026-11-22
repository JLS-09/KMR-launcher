using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using KMRLauncherMvvm.Models;

namespace KMRLauncherMvvm.Services;

public static class KspCompatibility
{
    private static readonly (int, int, int) LowestKsp = (0, 0, 0);
    private static readonly (int, int, int) HighestKsp = (int.MaxValue, int.MaxValue, int.MaxValue);

    public static bool IsCompatible(ModVersion version, GameVersionCriteria? criteria)
    {
        if (criteria is null) return true;

        var (modMin, modMax) = GetKspRange(version);

        foreach (var exact in criteria.ExactVersions)
        {
            var point = LowerBound(exact);
            if (point.CompareTo(modMin) >= 0 && point.CompareTo(modMax) <= 0) return true;
        }

        if (version.KspVersionStrict) return false;

        foreach (var prefix in criteria.MinorPrefixes)
        {
            if (LowerBound(prefix).CompareTo(modMax) <= 0 && UpperBound(prefix).CompareTo(modMin) >= 0) return true;
        }

        return false;
    }

    public static bool CoversMinor(ModVersion version, string minorPrefix)
    {
        var (modMin, modMax) = GetKspRange(version);
        return LowerBound(minorPrefix).CompareTo(modMax) <= 0 && UpperBound(minorPrefix).CompareTo(modMin) >= 0;
    }

    public static string DescribeKspRange(ModVersion version)
    {
        var strict = version.KspVersionStrict;
        var range = DescribeRange(version, v => strict ? v : KspVersions.ToMinorPrefix(v));
        return strict ? $"{range} strict" : range;
    }

    public static string DescribeKspRangeExact(ModVersion version) => DescribeRange(version, v => v);

    private static string DescribeRange(ModVersion version, Func<string, string> format)
    {
        if (version.KspVersion is not null) return IsAny(version.KspVersion) ? "any" : format(version.KspVersion);

        var min = version.KspVersionMin is null || IsAny(version.KspVersionMin) ? null : format(version.KspVersionMin);
        var max = version.KspVersionMax is null || IsAny(version.KspVersionMax) ? null : format(version.KspVersionMax);

        return (min, max) switch
        {
            (null, null) => "any",
            (_, null) => $"{min}+",
            (null, _) => $"≤ {max}",
            _ when min == max => min!,
            _ => $"{min}–{max}"
        };
    }

    public static bool CoversVersion(ModVersion version, string kspVersion)
    {
        var (modMin, modMax) = GetKspRange(version);
        var point = LowerBound(kspVersion);
        return point.CompareTo(modMin) >= 0 && point.CompareTo(modMax) <= 0;
    }

    private static ((int, int, int) min, (int, int, int) max) GetKspRange(ModVersion version)
    {
        if (version.KspVersion is not null)
        {
            return IsAny(version.KspVersion)
                ? (LowestKsp, HighestKsp)
                : (LowerBound(version.KspVersion), UpperBound(version.KspVersion));
        }

        var min = version.KspVersionMin is null || IsAny(version.KspVersionMin)
            ? LowestKsp
            : LowerBound(version.KspVersionMin);
        var max = version.KspVersionMax is null || IsAny(version.KspVersionMax)
            ? HighestKsp
            : UpperBound(version.KspVersionMax);

        return (min, max);
    }

    private static bool IsAny(string kspVersion) => kspVersion.Equals("any", StringComparison.OrdinalIgnoreCase);

    private static readonly ConcurrentDictionary<string, (int, int, int)> LowerBounds = new();
    private static readonly ConcurrentDictionary<string, (int, int, int)> UpperBounds = new();

    private static (int, int, int) LowerBound(string kspVersion) =>
        LowerBounds.GetOrAdd(kspVersion, ParseLowerBound);

    private static (int, int, int) UpperBound(string kspVersion) =>
        UpperBounds.GetOrAdd(kspVersion, ParseUpperBound);

    private static (int, int, int) ParseLowerBound(string kspVersion)
    {
        var parts = ParseKspVersion(kspVersion);
        if (parts is null) return LowestKsp;

        return (Part(parts, 0, 0), Part(parts, 1, 0), Part(parts, 2, 0));
    }

    private static (int, int, int) ParseUpperBound(string kspVersion)
    {
        var parts = ParseKspVersion(kspVersion);
        if (parts is null) return HighestKsp;

        return (Part(parts, 0, int.MaxValue), Part(parts, 1, int.MaxValue), Part(parts, 2, int.MaxValue));
    }

    private static int Part(List<int> parts, int index, int missing) => index < parts.Count ? parts[index] : missing;

    private static List<int>? ParseKspVersion(string kspVersion)
    {
        List<int> parts = [];

        foreach (var part in kspVersion.Trim().Split('.').Take(3))
        {
            if (!int.TryParse(part, out var number)) return null;
            parts.Add(number);
        }

        return parts.Count > 0 ? parts : null;
    }
}
