using CommunityToolkit.Mvvm.ComponentModel;
using KMRLauncherMvvm.Models;

namespace KMRLauncherMvvm.ViewModels.InstallMods;

public abstract partial class InstallModsStepViewModel : ViewModelBase
{
    public InstallModsData InstallModsData { get; }
    public abstract string Title { get; }
    public virtual bool CanGoNext => true;

    [ObservableProperty] private bool _shouldSkip;

    protected InstallModsStepViewModel(InstallModsData installModsData)
    {
        InstallModsData = installModsData;
    }
    
    public virtual void Prepare() { }

    public virtual void OnEntering(bool forward) { }

    public virtual void OnLeaving(bool forward) { }
}