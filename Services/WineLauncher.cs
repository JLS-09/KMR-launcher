using System.Diagnostics;
using System.IO;

namespace KMRLauncherMvvm.Services;

public static class WineLauncher
{
    private const string LauncherDataDir = "/home/jls/.config/kmrLauncher/";
    private const string WinePath = "/usr/bin/wine";

    public static Process Launch(string instanceId, string exePath, 
        string[]? gameArgs = null)
    {
        var prefixDir = Path.Combine(LauncherDataDir, "prefixes", instanceId);
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
}