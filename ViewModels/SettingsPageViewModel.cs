using KMRLauncherMvvm.Data;

namespace KMRLauncherMvvm.ViewModels;

public class SettingsPageViewModel : PageViewModel
{
    public string Test { get; set; } = "Settings";

    public SettingsPageViewModel()
    {
        PageName = ApplicationPageNames.Settings;
    }
}