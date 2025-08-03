using KMRLauncherMvvm.Data;

namespace KMRLauncherMvvm.ViewModels;

public partial class HomePageViewModel : PageViewModel
{
    public string Test { get; set; } = "Home";

    public HomePageViewModel()
    {
        PageName = ApplicationPageNames.Home;
    }
}