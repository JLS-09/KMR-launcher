using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Humanizer;
using KMRLauncherMvvm.Services;

namespace KMRLauncherMvvm.Models;

public abstract class InstanceTile;

public class Instance(string name, string rootPath, string version) : InstanceTile
{
    public string Name { get; set; } = name;
    public string RootPath { get; set; } = rootPath;
    public List<ModVersion> Mods { get; set; } = [];
    public string Version { get; set; } =  version;
    public int PlayTime  { get; set; } = 0;
    public DateTime LastPlayed { get; set; }
    public bool IsPrimary { get; set; } = false;
    
    public string PlayTimeHumanized => PlayTime <= 0 ? "0s" : new TimeSpan(0, 0, PlayTime)
        .Humanize(precision: 2, maxUnit: TimeUnit.Hour)
        .Replace(" hours", "h")
        .Replace(" minutes", "m")
        .Replace(" seconds", "s")
        .Replace(" hour", "h")
        .Replace(" minute", "m")
        .Replace(" second", "s");
    
    public string LastPlayedHumanized => PlayTime <= 0 ? "Never" : LastPlayed.Humanize()
        .Replace(" year", " yr")
        .Replace(" month", " mon")
        .Replace(" day", " d")
        .Replace(" hour", " hr")
        .Replace(" minute", " min")
        .Replace(" second", " sec");
    
    public string InstallSize => Helpers.BytesToString(Helpers.GetDirectorySize(RootPath));

    public async Task DeleteInstance()
    {
        await Task.Factory.StartNew(path => Directory.Delete((string)path, true), RootPath);
        App.Settings.Instances.Remove(this);
        SettingsService.Save(App.Settings);
    }

    public async Task LaunchInstanceWithWine()
    {
        var kspProcess = WineLauncher.Launch(Name, Path.Combine(RootPath, "KSP_x64.exe"));
        var startTime = kspProcess.StartTime;
        
        kspProcess.OutputDataReceived += (_, e) => 
        {
            if (e.Data != null) Console.WriteLine(e.Data);
        };
        kspProcess.ErrorDataReceived += (_, e) => 
        {
            if (e.Data != null) Console.Error.WriteLine(e.Data);
        };
        kspProcess.BeginOutputReadLine();
        kspProcess.BeginErrorReadLine();
        
        await kspProcess.WaitForExitAsync();
        
        var endTime = kspProcess.ExitTime;
        var playedSeconds = (endTime - startTime).TotalSeconds;

        PlayTime += (int) playedSeconds;
        
        LastPlayed = kspProcess.ExitTime;
        
        SettingsService.Save(App.Settings);

        SettingsService.Reload();
    }
}