using System;
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

    public override void OnEntering(InstallModsStepViewModel? previous)
    {
        foreach (var choosableVersion in InstallModsData.ChoosableVersions)
        {
            Console.WriteLine(choosableVersion);
        }
    }

    public override string Title => "Choose Modules";
}