using CommunityToolkit.Mvvm.Input;
using KMRLauncherMvvm.Data;
using KMRLauncherMvvm.Views;

namespace KMRLauncherMvvm.ViewModels;

public partial class InstancesPageViewModel : PageViewModel
{
    public string Test { get; set; } = "Instances";

    public InstancesPageViewModel()
    {
        PageName = ApplicationPageNames.Instances;
    }

    [RelayCommand]
    private void OpenNewInstanceWindow()
    {
        var window = new NewInstanceWindow
        {
            DataContext = new NewInstanceViewModel()
        };
        window.Show();
    }
}