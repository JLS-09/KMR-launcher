using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using KMRLauncherMvvm.Models;
using KMRLauncherMvvm.Services;

namespace KMRLauncherMvvm.ViewModels.InstallMods;

public partial class InstallModsSelectInstanceStepViewModel : InstallModsStepViewModel
{
    [ObservableProperty] private ObservableCollection<Instance> _instances;
    private readonly DependencyService _dependencyService;

    public InstallModsSelectInstanceStepViewModel(InstallModsData installModsData, DependencyService dependencyService)
        :
        base(installModsData)
    {
        _dependencyService = dependencyService;

        InstallModsData.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Models.InstallModsData.SelectedInstance))
            {
                _dependencyService.ResolveDependencies(InstallModsData);
                OnPropertyChanged(nameof(CanGoNext));
            }
        };

        _instances = App.Settings.Instances;
        InstallModsData.SelectedInstance = Instances.FirstOrDefault();
    }

    public override string Title => "Choose instance";
    public override bool CanGoNext => InstallModsData.SelectedInstance is not null;
}