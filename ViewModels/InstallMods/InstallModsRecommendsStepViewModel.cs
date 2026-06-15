using KMRLauncherMvvm.Models;

namespace KMRLauncherMvvm.ViewModels.InstallMods;

public class InstallModsRecommendsStepViewModel : InstallModsStepViewModel
{
    public InstallModsRecommendsStepViewModel(InstallModsData installModsData) : base(installModsData)
    {
    }

    public override string Title => "Choose Recommendations";
    public override bool CanGoNext => true;
}