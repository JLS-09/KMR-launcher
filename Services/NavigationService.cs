using System;
using KMRLauncherMvvm.Data;

namespace KMRLauncherMvvm.Services;

// Lets a page open another page, e.g. the Instances page's "Add mods" opens Discover for that instance
public class NavigationService
{
    public event Action<ApplicationPageNames, object?>? NavigationRequested;

    public void NavigateTo(ApplicationPageNames page, object? parameter = null) =>
        NavigationRequested?.Invoke(page, parameter);
}

// A page that takes a parameter when another page opens it
public interface INavigationTarget
{
    void OnNavigatedTo(object? parameter);
}
