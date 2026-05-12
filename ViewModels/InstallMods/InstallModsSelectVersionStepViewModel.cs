using KMRLauncherMvvm.Models;

namespace KMRLauncherMvvm.ViewModels.InstallMods;

public class InstallModsSelectVersionStepViewModel : InstallModsStepViewModel
{
    public InstallModsSelectVersionStepViewModel(InstallModsData installModsData) : base(installModsData)
    {InstallModsData.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Models.InstallModsData.SelectedInstance))
                OnPropertyChanged(nameof(CanGoNext));
        };
    }
    
    public override string Title => "Choose instance and version";
    public override bool CanGoNext => InstallModsData.SelectedInstance is not null && InstallModsData.RequestedVersions.Count > 0;
}