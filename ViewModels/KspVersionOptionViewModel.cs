using CommunityToolkit.Mvvm.ComponentModel;

namespace KMRLauncherMvvm.ViewModels;

public partial class KspVersionOptionViewModel : ObservableObject
{
    public string Version { get; }

    public bool IsInstanceVersion { get; }
    public bool CanToggle { get; }

    [ObservableProperty] private bool _isChecked;

    public KspVersionOptionViewModel(string version, bool isInstanceVersion, bool isChecked)
    {
        Version = version;
        IsInstanceVersion = isInstanceVersion;
        CanToggle = !isInstanceVersion;
        _isChecked = isInstanceVersion || isChecked;
    }
}