using KMRLauncherMvvm.Models;

namespace KMRLauncherMvvm.ViewModels;

public class NewInstanceViewModel : PageViewModel
{
    public AppSettings AppSettings
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged();
            }
        }
    }
    
    public NewInstanceViewModel()
    {
        AppSettings = App.Settings;
    }
}