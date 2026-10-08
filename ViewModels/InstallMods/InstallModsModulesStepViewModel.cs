using KMRLauncherMvvm.Models;

namespace KMRLauncherMvvm.ViewModels.InstallMods;

public partial class InstallModsModulesStepViewModel : InstallModsStepViewModel
{
    private ModListService _modListService;
    
    public InstallModsModulesStepViewModel(InstallModsData installModsData, ModListService modListService) : base(
        installModsData)
    {
        _modListService = modListService;
    }

    public override string Title => "Choose Modules";
}