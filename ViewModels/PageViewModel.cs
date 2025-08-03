using CommunityToolkit.Mvvm.ComponentModel;
using KMRLauncherMvvm.Data;

namespace KMRLauncherMvvm.ViewModels;

public partial class PageViewModel : ViewModelBase
{
    [ObservableProperty]
    private ApplicationPageNames _pageName;
}