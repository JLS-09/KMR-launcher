using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KMRLauncherMvvm.Data;
using KMRLauncherMvvm.Models;
using KMRLauncherMvvm.Services;

namespace KMRLauncherMvvm.ViewModels.InstallMods;

public enum InstallDialogPhase
{
    Wizard,
    Installing,
    Done,
    Failed
}

public enum InstallSource
{
    Discover,
    Updates
}

public enum StepState
{
    Done,
    Current,
    Upcoming
}

public sealed record StepperItem(int Number, string Label, StepState State, bool HasLineAfter)
{
    public bool IsDone => State == StepState.Done;
    public bool IsCurrent => State == StepState.Current;
    public bool LineReached => IsDone;
}

public sealed record PickedRow(string Name, string Version);

public sealed record StatTile(string Label, string Value);

public sealed record FailedRow(string Name, string StateText, double Progress, string Detail, bool IsFailed);

public partial class InstallDialogViewModel : ViewModelBase, IDisposable
{
    private const int ExtrasStep = 1;
    private const int ChoicesStep = 2;

    private readonly CompatibilityService _compatibilityService;
    private readonly DependencyService _dependencyService;
    private readonly ModListService _modListService;
    private readonly InstallService _installService;
    private readonly GameSessionService _session;
    private readonly NavigationService _navigation;
    private readonly DialogService _dialogs;
    private readonly InstanceService _instances;
    private readonly InstallSource _source;
    private readonly InstallModsData _data;
    private readonly TaskCompletionSource<bool> _result = new();

    private bool _extrasSkipped;
    private IReadOnlyList<ChoiceGroupViewModel> _choicesPreview = [];
    private ChangeCounts _runCounts = new(0, 0, 0, 0, 0);
    private HashSet<string> _installedBefore = [];

    public InstallModsViewModel? Wizard { get; }

    public Task<bool> Result => _result.Task;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsWizard), nameof(IsInstalling), nameof(IsDone), nameof(IsFailed),
        nameof(ShowClose), nameof(ShowSidePanel), nameof(ShowStepper))]
    private InstallDialogPhase _phase;

    public bool IsWizard => Phase == InstallDialogPhase.Wizard;
    public bool IsInstalling => Phase == InstallDialogPhase.Installing;
    public bool IsDone => Phase == InstallDialogPhase.Done;
    public bool IsFailed => Phase == InstallDialogPhase.Failed;
    public bool ShowClose => !IsInstalling;
    public bool ShowSidePanel => IsWizard;
    public bool ShowStepper => Wizard is not null;

    public string Title { get; }
    [ObservableProperty] private string _subtitle = "";
    [ObservableProperty] private IReadOnlyList<StepperItem> _steps = [];

    public ChangePanelViewModel Panel { get; } = new();
    public InstallResultViewModel Outcome { get; }
    public string PickedTitle { get; }
    public IReadOnlyList<PickedRow> Picked { get; }
    public bool HasPicked => Picked.Count > 0;

    [ObservableProperty] private bool _isReviewStep;
    [ObservableProperty] private string _installText = "Install";
    [ObservableProperty] private bool _canGoBack;

    [ObservableProperty] private InstallProgressViewModel? _progress;

    [ObservableProperty] [NotifyCanExecuteChangedFor(nameof(LaunchCommand))]
    private bool _isGameRunning;

    public Instance? Instance => Progress?.Run.Instance ?? _data.SelectedInstance;

    public InstallDialogViewModel(IReadOnlyList<PlannedModVersion> requested, Instance? target, InstallSource source,
        ModListService modListService, DependencyService dependencyService, CompatibilityService compatibilityService,
        InstallService installService, GameSessionService session, NavigationService navigation,
        DialogService dialogs, InstanceService instances, IReadOnlyList<ModVersion>? replaced = null)
        : this(modListService, dependencyService, compatibilityService, installService, session, navigation, dialogs,
            instances, source)
    {
        Title = "Install mods";
        Subtitle = source == InstallSource.Updates
            ? $"{InstallChanges.Plural(requested.Count, "update")} for {target?.Name}"
            : $"{InstallChanges.Plural(requested.Count, "mod")} picked in Discover";
        PickedTitle = source == InstallSource.Updates ? "UPDATES PICKED" : "PICKED IN DISCOVER";
        Picked =
        [
            .. requested.OrderBy(r => r.Version.Identifier, StringComparer.OrdinalIgnoreCase)
                .Select(r => new PickedRow(r.Version.Identifier, ShortVersion(r.Version.Version)))
        ];

        Wizard = new InstallModsViewModel([.. requested], modListService, dependencyService, compatibilityService,
            () => instances.Usable, target);
        _data = Wizard.InstallModsData;
        if (replaced is { Count: > 0 } && Wizard.Steps[0] is InstallModsSelectInstanceStepViewModel first)
        {
            _data.ReplacedModVersions = [.. replaced];
            first.Resolve();
        }

        foreach (var step in Wizard.Steps)
        {
            switch (step)
            {
                case InstallModsModulesStepViewModel modules:
                    modules.IsCached = IsCached;
                    modules.AbstractOf = AbstractOf;
                    break;
                case InstallModsSummaryStepViewModel summary:
                    summary.IsCached = IsCached;
                    break;
                case InstallModsSelectInstanceStepViewModel select:
                    select.Resolved += OnResolved;
                    break;
            }
        }

        Wizard.StepChanged += OnStepChanged;
        Wizard.Finished += _1 => _ = StartInstallAsync();
        Wizard.PropertyChanged += OnWizardPropertyChanged;

        Phase = InstallDialogPhase.Wizard;
        OnResolved();
    }

    public InstallDialogViewModel(Instance instance, IReadOnlyList<ModVersion> removals,
        ModListService modListService, DependencyService dependencyService, CompatibilityService compatibilityService,
        InstallService installService, GameSessionService session, NavigationService navigation,
        DialogService dialogs, InstanceService instances)
        : this(modListService, dependencyService, compatibilityService, installService, session, navigation, dialogs,
            instances, InstallSource.Updates)
    {
        Title = "Remove mods";
        PickedTitle = "";
        Picked = [];
        _data = new InstallModsData
        {
            SelectedInstance = instance,
            RequestedRemoveModVersions = [.. removals.Select(m => new PlannedModVersion { Version = m })]
        };
        _ = StartInstallAsync();
    }

    private InstallDialogViewModel(ModListService modListService, DependencyService dependencyService,
        CompatibilityService compatibilityService, InstallService installService, GameSessionService session,
        NavigationService navigation, DialogService dialogs, InstanceService instances, InstallSource source)
    {
        _instances = instances;
        Outcome = new InstallResultViewModel(v => installService.FindCached(v) is not null);
        _modListService = modListService;
        _dependencyService = dependencyService;
        _compatibilityService = compatibilityService;
        _installService = installService;
        _session = session;
        _navigation = navigation;
        _dialogs = dialogs;
        _source = source;
        _data = new InstallModsData();
        Title = "";
        PickedTitle = "";
        Picked = [];

        IsGameRunning = session.IsRunning;
        session.Changed += OnSessionChanged;
    }

    public void Dispose() => _session.Changed -= OnSessionChanged;

    private void OnSessionChanged() => IsGameRunning = _session.IsRunning;

    private bool IsCached(ModVersion version) =>
        InstallChanges.NeedsDownload(version) && _installService.FindCached(version) is not null;

    private string? AbstractOf(string identifier) =>
        _modListService.Mods?.FirstOrDefault(m => m.Id == identifier)?.Abstract;

    private static string ShortVersion(string version) =>
        version.Length <= 12 ? version : InstalledMods.ShortNextVersion(version, version);


    private void OnWizardPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(InstallModsViewModel.CurrentStep)) OnStepChanged();
    }

    private void OnStepChanged()
    {
        if (Wizard is null) return;

        if (Wizard.CurrentStep is InstallModsModulesStepViewModel modules)
        {
            modules.PropertyChanged -= OnChoicesChanged;
            modules.PropertyChanged += OnChoicesChanged;
        }

        IsReviewStep = Wizard.IsLastStep;
        if (Wizard.CurrentStep is InstallModsSummaryStepViewModel summary) InstallText = summary.InstallText;
        CanGoBack = !Wizard.IsFirstStep;
        if (Wizard.CurrentIndex < ChoicesStep) PreviewChoices();
        UpdateStepper();
        UpdatePanel();
    }

    private void OnChoicesChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(InstallModsModulesStepViewModel.ChoicesLeft)
            or nameof(InstallModsModulesStepViewModel.HasProblems))
            UpdatePanel();
    }

    private void OnResolved()
    {
        if (Wizard is null) return;

        var extras = Wizard.Steps[ExtrasStep];
        extras.Prepare();
        _extrasSkipped = extras.ShouldSkip;

        PreviewChoices();
        UpdateStepper();
        UpdatePanel();
    }

    private void PreviewChoices()
    {
        var copy = new InstallModsData
        {
            SelectedInstance = _data.SelectedInstance,
            RequestedModVersions = [.. _data.RequestedModVersions],
            FinalModList = [.. _data.FinalModList],
            RequestedRemoveModVersions = [.. _data.RequestedRemoveModVersions],
            ReplacedModVersions = [.. _data.ReplacedModVersions],
            ChoosableVersions = [.. _data.ChoosableVersions],
            ModsWithIgnoredRecommendations = [.. _data.ModsWithIgnoredRecommendations],
            Problems = [.. _data.Problems]
        };

        try
        {
            var preview = new InstallModsModulesStepViewModel(copy, _compatibilityService, _dependencyService);
            preview.Prepare();
            _choicesPreview = [.. preview.Choices];
        }
        catch (Exception)
        {
            _choicesPreview = [];
        }

        if (Wizard?.Steps[0] is InstallModsSelectInstanceStepViewModel select)
            select.ChoicesHint = ChoicesHint(_choicesPreview, ChoicesStepNumber());
    }

    public static string? ChoicesHint(IReadOnlyList<ChoiceGroupViewModel> choices, int stepNumber)
    {
        if (choices.Count == 0) return null;
        if (choices.Count > 1)
            return $"{choices.Count} mods still need a choice. You pick them in step {stepNumber}.";

        var choice = choices[0];
        var who = choice.Requirers.Count > 0 ? InstallChanges.JoinNames([.. choice.Requirers]) : "A mod";
        var also = choice.Requirers.Count > 1 ? "also need" : "also needs";
        return $"{who} {also} {choice.WantedText}. You pick it in step {stepNumber}.";
    }

    private bool IsSkipped(int index)
    {
        if (Wizard is null) return false;
        if (index == Wizard.CurrentIndex) return false;
        if (index < Wizard.CurrentIndex) return Wizard.Steps[index].ShouldSkip;

        return index switch
        {
            ExtrasStep => _extrasSkipped,
            ChoicesStep => _choicesPreview.Count == 0,
            _ => false
        };
    }

    private int ChoicesStepNumber() => Enumerable.Range(0, ChoicesStep).Count(i => !IsSkipped(i)) + 1;

    private void UpdateStepper()
    {
        if (Wizard is null) return;

        var installing = !IsWizard;
        var shown = Enumerable.Range(0, Wizard.Steps.Count).Where(i => !IsSkipped(i)).ToList();
        List<StepperItem> items = [];

        foreach (var index in shown)
        {
            var state = installing || index < Wizard.CurrentIndex ? StepState.Done
                : index == Wizard.CurrentIndex ? StepState.Current
                : StepState.Upcoming;
            items.Add(new StepperItem(items.Count + 1, Wizard.Steps[index].Title, state, true));
        }

        var installState = IsDone ? StepState.Done : installing ? StepState.Current : StepState.Upcoming;
        items.Add(new StepperItem(items.Count + 1, "Install", installState, false));
        Steps = items;
    }

    private void UpdatePanel()
    {
        var choicesLeft = Wizard is null ? 0
            : Wizard.CurrentStep is InstallModsModulesStepViewModel modules ? modules.ChoicesLeft
            : Wizard.CurrentIndex < ChoicesStep ? _choicesPreview.Count
            : 0;
        Panel.Update(_data, IsCached, choicesLeft);
    }

    [RelayCommand]
    private void Close()
    {
        if (IsInstalling) return;

        _result.TrySetResult(false);
        _dialogs.Close(this);
    }
    
    private async Task StartInstallAsync()
    {
        if (_data.SelectedInstance is not { } instance) return;

        _installedBefore = [.. instance.Mods.Select(m => m.Identifier)];
        _runCounts = InstallChanges.Count(InstallChanges.Rows(_data, IsCached));
        var run = InstallRun.Create(instance, _data);
        Progress = new InstallProgressViewModel(_installService, run, _instances.Settings.MaxParallelDownloads);

        var installs = _runCounts.New + _runCounts.Updates;
        Subtitle = installs > 0
            ? $"{InstallChanges.Plural(installs, "mod")} into {instance.Name}"
            : $"{InstallChanges.Plural(_runCounts.Removed, "mod")} from {instance.Name}";
        Phase = InstallDialogPhase.Installing;
        UpdateStepper();

        if (await Progress.RunAsync())
        {
            Outcome.ShowDone(instance, Progress, _runCounts, Wizard is not null && _source == InstallSource.Discover
                ? "Your selection in Discover was cleared."
                : "");
            Phase = InstallDialogPhase.Done;
            UpdateStepper();
            _result.TrySetResult(true);
        }
        else
        {
            Outcome.ShowFailed(instance, Progress, _installedBefore);
            Phase = InstallDialogPhase.Failed;
            UpdateStepper();
        }
    }

    [RelayCommand]
    private void CancelInstall() => Progress?.CancelCommand.Execute(null);

    [RelayCommand]
    private void Retry() => _ = StartInstallAsync();

    public bool CanGoBackToReview => Wizard is not null;

    [RelayCommand]
    private void BackToReview()
    {
        if (Wizard is null) return;

        Phase = InstallDialogPhase.Wizard;
        Wizard.GoToLastStep();
    }

    private bool CanLaunch() => !IsGameRunning;

    [RelayCommand(CanExecute = nameof(CanLaunch))]
    private void Launch()
    {
        if (Instance is not { } instance) return;

        _ = _session.LaunchAsync(instance);
        Close();
    }

    [RelayCommand]
    private void ShowMods()
    {
        if (Instance is not { } instance) return;

        Close();
        _navigation.NavigateTo(ApplicationPageNames.Instances, instance);
    }
}
