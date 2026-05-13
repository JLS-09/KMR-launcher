using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using KMRLauncherMvvm.Models;

namespace KMRLauncherMvvm.ViewModels.InstallMods;

public partial class InstallModsSelectVersionStepViewModel : InstallModsStepViewModel
{
    [ObservableProperty] private ObservableCollection<Instance> _instances;
    
    public InstallModsSelectVersionStepViewModel(InstallModsData installModsData) : base(installModsData)
    {
        InstallModsData.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Models.InstallModsData.SelectedInstance))
                OnPropertyChanged(nameof(CanGoNext));
        };

        _instances = App.Settings.Instances;

        InstallModsData.SelectedInstance = Instances.FirstOrDefault();
        InstallModsData.RequestedVersion = InstallModsData.AvailableVersions.FirstOrDefault();
    }
    
    public override string Title => "Choose instance and version";
    public override bool CanGoNext => InstallModsData.SelectedInstance is not null && InstallModsData.RequestedVersion is not null;
}