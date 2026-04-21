using CommunityToolkit.Mvvm.Input;
using KMRLauncherMvvm.Data;
using KMRLauncherMvvm.Services;
using KMRLauncherMvvm.Views;

namespace KMRLauncherMvvm.ViewModels;

public partial class InstancesPageViewModel : PageViewModel
{
    public string Test { get; set; } = "Instances";
    private ZipService ZipService { get; set; }

    public InstancesPageViewModel(ZipService zipService)
    {
        PageName = ApplicationPageNames.Instances;
        ZipService = zipService;
    }

    public InstancesPageViewModel()
    {
        
    }

    [RelayCommand]
    private void OpenNewInstanceWindow()
    {
        var window = new NewInstanceWindow
        {
            DataContext = new NewInstanceViewModel(ZipService)
        };
        window.Show();
    }
}