using System;
using System.Diagnostics;
using System.IO;

namespace KMRLauncherMvvm.Services;

public static class WineLauncher
{
    private const string WinePath = "/usr/bin/wine";
    private const string WineServerPath = "/usr/bin/wineserver";

    // prefixDir: the instance's Wine prefix (Instance.PrefixPath, see WinePrefixes)
    public static Process Launch(string prefixDir, string exePath,
        string[]? gameArgs = null)
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
                ["WINEARCH"] = "win64",
                ["WINEDEBUG"] = "-all"
            }
        };

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