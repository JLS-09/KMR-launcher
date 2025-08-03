using KMRLauncherMvvm.Data;

namespace KMRLauncherMvvm.ViewModels;

public partial class DiscoverPageViewModel : PageViewModel
{
    public string Test { get; set; } = "Discover";

    public DiscoverPageViewModel()
    {
        PageName = ApplicationPageNames.Discover;
    }
}