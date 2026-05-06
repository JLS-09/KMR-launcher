using System;
using System.Diagnostics;
using System.IO;

namespace KMRLauncherMvvm.Services;

public static class ProtonLauncher
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
        };

        psi.Environment["WINEPREFIX"] = prefixDir;
        psi.Environment["WINEARCH"] = "win64";
        // Quiet down Wine's debug spam
        psi.Environment["WINEDEBUG"] = "-all";

        psi.ArgumentList.Add(exePath);
        if (gameArgs != null)
            foreach (var arg in gameArgs) psi.ArgumentList.Add(arg);

        return Process.Start(psi)!;
    }

    public static Process InitPrefix(string instanceId)
    {
        var prefixDir = Path.Combine(LauncherDataDir, "prefixes", instanceId);
        Directory.CreateDirectory(prefixDir);

        var psi = new ProcessStartInfo
        {
            FileName = WinePath,
            ArgumentList = { "-w" },
            UseShellExecute = false,
        };
        psi.Environment["WINEPREFIX"] = prefixDir;
        psi.Environment["WINEARCH"] = "win64";
        psi.ArgumentList.Add("wineboot");
        psi.ArgumentList.Add("--init");

        return Process.Start(psi)!;
    }
}