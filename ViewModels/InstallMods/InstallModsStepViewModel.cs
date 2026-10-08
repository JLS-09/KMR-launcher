using KMRLauncherMvvm.Models;

namespace KMRLauncherMvvm.ViewModels.InstallMods;

public abstract class InstallModsStepViewModel : ViewModelBase
{
    public InstallModsData InstallModsData { get; }
    public abstract string Title { get; }
    public virtual bool CanGoNext => true;

    protected InstallModsStepViewModel(InstallModsData installModsData)
    {
        InstallModsData = installModsData;
    }
    
    public virtual void OnEntering(InstallModsStepViewModel? previous) { }
    public virtual void OnLeaving(InstallModsStepViewModel? next) { }
}