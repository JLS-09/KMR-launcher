using System;
using System.IO;
using System.Reflection;

namespace KMRLauncherMvvm.Services;

public static class AppInfo
{
    public const string SourceUrl = "https://github.com/JLS-09/KMR-launcher";

    public static string DataFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "kmrLauncher");

    public static string VersionText => Describe(typeof(AppInfo).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);

    public static string Describe(string? informationalVersion)
    {
        if (string.IsNullOrWhiteSpace(informationalVersion)) return "dev build";

        var plus = informationalVersion.IndexOf('+');
        var version = plus < 0 ? informationalVersion : informationalVersion[..plus];
        var commit = plus < 0 ? "" : informationalVersion[(plus + 1)..];
        var name = version is "1.0.0" or "" ? "dev build" : version;
        return commit.Length >= 7 ? $"{name} · {commit[..7]}" : name;
    }
}
