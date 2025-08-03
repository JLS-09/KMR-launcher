using System;
using KMRLauncherMvvm.Data;
using KMRLauncherMvvm.ViewModels;

namespace KMRLauncherMvvm.Factories;

public class PageFactory(Func<ApplicationPageNames, PageViewModel> factory)
{
    public PageViewModel GetPageViewModel(ApplicationPageNames pageName) => factory.Invoke(pageName);
}