using System.Diagnostics;
using System.IO;

namespace KMRLauncherMvvm.Services;

public static class ProtonLauncher
{
    private const string ProtonPath = "/home/jls/Downloads/GE-Proton9-4/proton";
    private const string LauncherDataDir = "/home/jls/.config/kmrLauncher/";

    public static Process Launch(string instanceId, string exePath, 
        string[]? gameArgs = null)
    {
        var prefixDir = Path.Combine(LauncherDataDir, "prefixes", instanceId);
        Directory.CreateDirectory(prefixDir);
        
        var psi = new ProcessStartInfo
        {
            FileName = ProtonPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = Path.GetDirectoryName(exePath)!,
            Environment =
            {
                ["STEAM_COMPAT_DATA_PATH"] = prefixDir,
                ["STEAM_COMPAT_CLIENT_INSTALL_PATH"] = LauncherDataDir
            }
        };

        psi.ArgumentList.Add("run");
        psi.ArgumentList.Add(exePath);
        
        if (gameArgs == null) return Process.Start(psi)!;
        
        foreach (var arg in gameArgs) psi.ArgumentList.Add(arg);

        return Process.Start(psi)!;
    }
}