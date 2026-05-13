using System;
using System.Collections.Generic;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KMRLauncherMvvm.Models;
using KMRLauncherMvvm.Services.Api;

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
    
    public event Action<InstallModsData>? Finished;
    public event Action? Cancelled;
    
    public InstallModsViewModel(List<ModVersion> versions)
    {
        _steps = [new InstallModsSelectVersionStepViewModel(InstallModsData)];
        SetStep(0, fromIndex: -1);
        
        foreach (var version in versions)
            InstallModsData.AvailableVersions.Add(version);
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
    private void Next() => SetStep(_index + 1, _index);

    [RelayCommand(CanExecute = nameof(CanGoBack))]
    private void Back() => SetStep(_index - 1, _index);

    [RelayCommand(CanExecute = nameof(CanFinish))]
    private void Finish() => Finished?.Invoke(InstallModsData);

    [RelayCommand]
    private void Cancel() => Cancelled?.Invoke();
    
    private void SetStep(int newIndex, int fromIndex)
    {
        var previous = fromIndex >= 0 ? _steps[fromIndex] : null;
        var next = _steps[newIndex];

        previous?.OnLeaving(next);
        _index = newIndex;
        CurrentStep = next;
        next.OnEntering(previous);
    }
}