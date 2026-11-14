using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KMRLauncherMvvm.Models;
using KMRLauncherMvvm.Services;

namespace KMRLauncherMvvm.ViewModels.InstallMods;

public partial class InstallModsViewModel : ViewModelBase
{
    private readonly List<InstallModsStepViewModel> _steps;
    private int _index;

    public InstallModsData InstallModsData { get; } = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NextCommand))]
    [NotifyCanExecuteChangedFor(nameof(BackCommand))]
    [NotifyCanExecuteChangedFor(nameof(FinishCommand))]
    private InstallModsStepViewModel _currentStep = null!;

    [ObservableProperty] private bool _isFirstStep = true;
    [ObservableProperty] private List<ProgressSegment> _progressSegments = [];
    [ObservableProperty] private bool _isLastStep;
    public event Action<InstallModsData>? Finished;
    public event Action? Cancelled;

    public InstallModsViewModel(List<PlannedModVersion> mods, ModListService modListService,
        DependencyService dependencyService, CompatibilityService compatibilityService)
    {
        InstallModsData.RequestedModVersions = new ObservableCollection<PlannedModVersion>(mods);

        _steps =
        [
            new InstallModsSelectInstanceStepViewModel(InstallModsData, dependencyService),
            new InstallModsRecommendsStepViewModel(InstallModsData, modListService, compatibilityService,
                dependencyService),
            new InstallModsModulesStepViewModel(InstallModsData, compatibilityService, dependencyService),
            new InstallModsSummaryStepViewModel(InstallModsData)
        ];
        ShowStep(0, forward: true);
    }

    public InstallModsViewModel()
    {
    }

    partial void OnCurrentStepChanged(InstallModsStepViewModel? oldValue, InstallModsStepViewModel newValue)
    {
        if (oldValue is not null)
            oldValue.PropertyChanged -= OnStepPropertyChanged;
        newValue.PropertyChanged += OnStepPropertyChanged;
    }

    private void OnStepPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(InstallModsStepViewModel.CanGoNext)) return;

        NextCommand.NotifyCanExecuteChanged();
        FinishCommand.NotifyCanExecuteChanged();
    }

    private bool IsLast => _index == _steps.Count - 1;
    private bool IsFirst => _index == 0;

    private bool CanGoNext() => CurrentStep.CanGoNext && !IsLast;
    private bool CanGoBack() => !IsFirst;
    private bool CanFinish() => CurrentStep.CanGoNext && IsLast;

    [RelayCommand(CanExecute = nameof(CanGoNext))]
    private void Next()
    {
        _steps[_index].OnLeaving(forward: true);

        var target = _index + 1;
        _steps[target].Prepare();
        while (_steps[target].ShouldSkip && target < _steps.Count - 1)
        {
            _steps[target].OnLeaving(forward: true);
            target++;
            _steps[target].Prepare();
        }

        ShowStep(target, forward: true);
    }

    [RelayCommand(CanExecute = nameof(CanGoBack))]
    private void Back()
    {
        var target = _index - 1;
        while (target > 0 && _steps[target].ShouldSkip)
            target--;

        _steps[_index].OnLeaving(forward: false);
        ShowStep(target, forward: false);
    }

    [RelayCommand(CanExecute = nameof(CanFinish))]
    private void Finish() => Finished?.Invoke(InstallModsData);

    [RelayCommand]
    private void Cancel() => Cancelled?.Invoke();

    private void ShowStep(int index, bool forward)
    {
        _index = index;
        IsFirstStep = index == 0;
        IsLastStep = index == _steps.Count - 1;
        CurrentStep = _steps[index];
        CurrentStep.OnEntering(forward);
        UpdateProgress();
    }

    private void UpdateProgress()
    {
        ProgressSegments =
        [
            .. _steps
                .Select((step, i) => (step, i))
                .Where(s => s.i == _index || !s.step.ShouldSkip)
                .Select(s => new ProgressSegment(s.i <= _index))
        ];
    }
}

public record ProgressSegment(bool IsReached);