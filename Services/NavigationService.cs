using System;
using KMRLauncherMvvm.Data;

namespace KMRLauncherMvvm.Services;

public class NavigationService
{
    public event Action<ApplicationPageNames, object?>? NavigationRequested;

    public void NavigateTo(ApplicationPageNames page, object? parameter = null) =>
        NavigationRequested?.Invoke(page, parameter);
}

public interface INavigationTarget
{
    void OnNavigatedTo(object? parameter);
}