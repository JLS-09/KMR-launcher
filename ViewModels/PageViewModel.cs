using System;
using CommunityToolkit.Mvvm.ComponentModel;
using KMRLauncherMvvm.Data;

namespace KMRLauncherMvvm.ViewModels;

public partial class PageViewModel : ViewModelBase, IDisposable
{
    [ObservableProperty]
    private ApplicationPageNames _pageName;

    public virtual void Dispose()
    {
    }
}