using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using KMRLauncherMvvm.Models;
using KMRLauncherMvvm.Services;

namespace KMRLauncherMvvm.ViewModels.InstallMods;

public partial class InstallModsSelectInstanceStepViewModel : InstallModsStepViewModel
{
    [ObservableProperty] private ObservableCollection<Instance> _instances;
    [ObservableProperty] private bool _hasProblems;
    [ObservableProperty] private bool _canContinue;

    private readonly DependencyService _dependencyService;

    public InstallModsSelectInstanceStepViewModel(InstallModsData installModsData, DependencyService dependencyService)
        :
        base(installModsData)
    {
        _dependencyService = dependencyService;

        InstallModsData.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Models.InstallModsData.SelectedInstance))
                Resolve();
        };

        _instances = App.Settings.Instances;
        InstallModsData.SelectedInstance = Instances.FirstOrDefault();
    }

    private void Resolve()
    {
        try
        {
            _dependencyService.ResolveDependencies(InstallModsData);
        }
        catch (Exception e)
        {
            InstallModsData.Problems = [new ResolutionProblem { Mod = "", Message = $"Unexpected error: {e.Message}" }];
        }

        HasProblems = InstallModsData.Problems.Count > 0;
        CanContinue = InstallModsData.SelectedInstance is not null && !HasProblems;
    }

    partial void OnCanContinueChanged(bool value) => OnPropertyChanged(nameof(CanGoNext));

    public override string Title => "Choose instance";
    public override bool CanGoNext => CanContinue;
}