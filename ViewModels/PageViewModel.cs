using CommunityToolkit.Mvvm.ComponentModel;
using KMRLauncherMvvm.Data;

namespace KMRLauncherMvvm.ViewModels;

public partial class PageViewModel : ViewModelBase
{
    [ObservableProperty]
    private ApplicationPageNames _pageName;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsShown))]
    private bool _isActive;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsShown))]
    private bool _isWarming;

    public bool IsShown => IsActive || IsWarming;
}
