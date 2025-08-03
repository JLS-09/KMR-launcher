using KMRLauncherMvvm.Data;

namespace KMRLauncherMvvm.ViewModels;

public partial class InstancesPageViewModel : PageViewModel
{
    public string Test { get; set; } = "Instances";

    public InstancesPageViewModel()
    {
        PageName = ApplicationPageNames.Instances;
    }
}