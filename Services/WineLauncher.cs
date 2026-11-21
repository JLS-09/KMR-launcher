using System;
using System.Diagnostics;
using System.IO;
using KMRLauncherMvvm.Models;

namespace KMRLauncherMvvm.Services;

public static class WineLauncher
{
    private const UnixFileMode Executable =
        UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;

    private static string WinePath => FindOnPath("wine") ?? "/usr/bin/wine";
    private static string WineServerPath => FindOnPath("wineserver") ?? "/usr/bin/wineserver";

    public static string? FindOnPath(string name, string? path = null)
    {
        var folders = (path ?? Environment.GetEnvironmentVariable("PATH") ?? "")
            .Split(':', StringSplitOptions.RemoveEmptyEntries);
        foreach (var folder in folders)
        {
            var candidate = Path.Combine(folder, name);
            if (!File.Exists(candidate)) continue;
            if (OperatingSystem.IsWindows() || (File.GetUnixFileMode(candidate) & Executable) != 0) return candidate;
        }

        return null;
    }

    public static string? DebugValue(WineDebugLevel level) => level switch
    {
        WineDebugLevel.Off => "-all",
        WineDebugLevel.ErrorsOnly => "-all,err+all",
        _ => null
    };

    public static Process Launch(string prefixDir, string exePath,
        string[]? gameArgs = null, WineDebugLevel debug = WineDebugLevel.Off)
    {
        Directory.CreateDirectory(prefixDir);

        var psi = new ProcessStartInfo
        {
            FileName = WinePath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = Path.GetDirectoryName(exePath)!,
            Environment =
            {
                ["WINEPREFIX"] = prefixDir,
                ["WINEARCH"] = "win64"
            }
        };

        if (DebugValue(debug) is { } value) psi.Environment["WINEDEBUG"] = value;
        else psi.Environment.Remove("WINEDEBUG");

        psi.ArgumentList.Add(exePath);

        if (gameArgs == null) return Process.Start(psi)!;

        foreach (var arg in gameArgs) psi.ArgumentList.Add(arg);

        return Process.Start(psi)!;
    }

    public static void Stop(Process process, string prefixDir)
    {
        try
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
        }

        if (!File.Exists(WineServerPath) || !Directory.Exists(prefixDir)) return;

        var psi = new ProcessStartInfo { FileName = WineServerPath, UseShellExecute = false };
        psi.ArgumentList.Add("-k");
        psi.Environment["WINEPREFIX"] = prefixDir;
        Process.Start(psi)?.WaitForExit(5000);
    }
}