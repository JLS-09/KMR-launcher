using System.IO;
using KMRLauncherMvvm.Models;
using KMRLauncherMvvm.Services;
using KMRLauncherMvvm.ViewModels.Instances;

namespace KMRLauncherMvvm.ViewModels.Settings;

public class KspZipRow
{
    public KspZip Zip { get; }
    public string Version => Zip.Version;
    public string Path => Zip.Path;
    public string PathText { get; }
    public bool IsMissing { get; }
    public string SizeText { get; }
    public string RemoveLabel => $"Remove KSP {Version} zip";

    public KspZipRow(KspZip zip)
    {
        Zip = zip;
        PathText = InstanceTexts.ShortPath(zip.Path);
        var file = new FileInfo(zip.Path);
        IsMissing = !file.Exists;
        SizeText = IsMissing ? "missing" : InstalledMods.FormatSize(file.Length);
    }
}
