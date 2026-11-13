using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace KMRLauncherMvvm.ViewModels;

public partial class KspVersionGroupViewModel : ObservableObject
{
    public string Prefix { get; }
    public string AllowAllText { get; }
    public List<KspVersionOptionViewModel> Versions { get; }

    [ObservableProperty] private bool _allowAll;

    public KspVersionGroupViewModel(string prefix, bool allowAll, List<KspVersionOptionViewModel> versions)
    {
        Prefix = prefix;
        AllowAllText = $"All {prefix}.x";
        Versions = versions;
        _allowAll = allowAll;
    }

    public IEnumerable<string> ToEntries()
    {
        if (AllowAll) yield return Prefix;

        foreach (var version in Versions)
        {
            if (version.IsChecked && !version.IsInstanceVersion)
                yield return version.Version;
        }
    }
}