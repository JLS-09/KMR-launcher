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

public partial class InstallDialogViewModel : ViewModelBase
{
    private const int ExtrasStep = 1;
    private const int ChoicesStep = 2;
    private const int FailedRowsShown = 5;

    private readonly CompatibilityService _compatibilityService;
    private readonly DependencyService _dependencyService;
    private readonly ModListService _modListService;
    private readonly InstallService _installService;
    private readonly GameSessionService _session;
    private readonly NavigationService _navigation;
    private readonly DialogService _dialogs;
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

    [ObservableProperty] private string _panelInstanceName = "";
    [ObservableProperty] private string _panelKspText = "";
    [ObservableProperty] private string _newCount = "0";
    [ObservableProperty] private string _updatesCount = "0";
    [ObservableProperty] private string _removedCount = "0";
    [ObservableProperty] private bool _hasRemoved;
    [ObservableProperty] private string _downloadText = "0 MB";
    [ObservableProperty] private string _cachedText = "";
    [ObservableProperty] private bool _hasCached;
    [ObservableProperty] private string _problemsText = "No problems";
    [ObservableProperty] private bool _hasProblems;
    [ObservableProperty] private string _choicesLeftText = "";
    [ObservableProperty] private bool _hasChoicesLeft;
    public string PickedTitle { get; }
    public IReadOnlyList<PickedRow> Picked { get; }
    public bool HasPicked => Picked.Count > 0;

    [ObservableProperty] private bool _isReviewStep;
    [ObservableProperty] private string _installText = "Install";
    [ObservableProperty] private bool _canGoBack;

    [ObservableProperty] private InstallProgressViewModel? _progress;
    [ObservableProperty] private string _doneTitle = "";
    [ObservableProperty] private string _doneSummary = "";
    [ObservableProperty] private IReadOnlyList<StatTile> _doneTiles = [];
    [ObservableProperty] private string _launchText = "";
    [ObservableProperty] private string _selectionNote = "";
    [ObservableProperty] private string _failedTitle = "";
    [ObservableProperty] private string _failedSummary = "";
    [ObservableProperty] private string _errorText = "";
    [ObservableProperty] private bool _showError;
    [ObservableProperty] private IReadOnlyList<FailedRow> _failedRows = [];
    [ObservableProperty] private string _failedRowsCount = "";
    [ObservableProperty] private string _moreFailedText = "";
    [ObservableProperty] private bool _hasMoreFailed;
    [ObservableProperty] private string _cacheHint = "";

    [ObservableProperty] [NotifyCanExecuteChangedFor(nameof(LaunchCommand))]
    private bool _isGameRunning;

    public Instance? Instance => Progress?.Run?.Instance ?? _data.SelectedInstance;

    public InstallDialogViewModel(IReadOnlyList<PlannedModVersion> requested, Instance? target, InstallSource source,
        ModListService modListService, DependencyService dependencyService, CompatibilityService compatibilityService,
        InstallService installService, GameSessionService session, NavigationService navigation,
        DialogService dialogs, IReadOnlyList<ModVersion>? replaced = null)
        : this(modListService, dependencyService, compatibilityService, installService, session, navigation, dialogs,
            source)
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
            target);
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
        DialogService dialogs)
        : this(modListService, dependencyService, compatibilityService, installService, session, navigation, dialogs,
            InstallSource.Updates)
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
        NavigationService navigation, DialogService dialogs, InstallSource source)
    {
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
        session.Changed += () => IsGameRunning = session.IsRunning;
    }

    // Design-time
    public InstallDialogViewModel()
    {
        _modListService = null!;
        _dependencyService = null!;
        _compatibilityService = null!;
        _installService = null!;
        _session = null!;
        _navigation = null!;
        _dialogs = null!;
        _data = new InstallModsData();
        Title = "Install mods";
        PickedTitle = "Picked in Discover";
        Picked = [];
    }

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
        var instance = _data.SelectedInstance;
        PanelInstanceName = instance?.Name ?? "No instance";
        PanelKspText = instance is null ? "" : $"KSP {instance.CompatibleSummary}";

        var rows = InstallChanges.Rows(_data, IsCached);
        var counts = InstallChanges.Count(rows);
        NewCount = counts.New.ToString();
        UpdatesCount = counts.Updates.ToString();
        RemovedCount = counts.Removed.ToString();
        HasRemoved = counts.Removed > 0;
        DownloadText = InstalledMods.FormatSize(counts.DownloadBytes);
        HasCached = counts.CachedBytes > 0;
        CachedText = InstalledMods.FormatSize(counts.CachedBytes);

        var problems = _data.Problems.Count;
        HasProblems = problems > 0;
        ProblemsText = problems == 0 ? "No problems" : InstallChanges.Plural(problems, "problem");

        var choicesLeft = Wizard is null ? 0
            : Wizard.CurrentStep is InstallModsModulesStepViewModel modules ? modules.ChoicesLeft
            : Wizard.CurrentIndex < ChoicesStep ? _choicesPreview.Count
            : 0;
        HasChoicesLeft = choicesLeft > 0;
        ChoicesLeftText = choicesLeft == 1 ? "1 choice left" : $"{choicesLeft} choices left";
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
        Progress = new InstallProgressViewModel(_installService, run, App.Settings.MaxParallelDownloads);

        var installs = _runCounts.New + _runCounts.Updates;
        Subtitle = installs > 0
            ? $"{InstallChanges.Plural(installs, "mod")} into {instance.Name}"
            : $"{InstallChanges.Plural(_runCounts.Removed, "mod")} from {instance.Name}";
        Phase = InstallDialogPhase.Installing;
        UpdateStepper();

        if (await Progress.RunAsync())
        {
            ShowDone(instance, Progress);
            _result.TrySetResult(true);
        }
        else
        {
            ShowFailed(instance, Progress);
        }
    }

    private void ShowDone(Instance instance, InstallProgressViewModel progress)
    {
        var jobs = progress.Run?.Jobs ?? [];
        var installs = _runCounts.New + _runCounts.Updates;
        var took = $"took {InstallChanges.Duration(progress.Elapsed)}";

        if (installs > 0)
        {
            DoneTitle = $"Installed into {instance.Name}";
            List<string> parts = [];
            if (_runCounts.New > 0) parts.Add($"{_runCounts.New} new");
            if (_runCounts.Updates > 0) parts.Add($"{_runCounts.Updates} updated");
            if (_runCounts.Removed > 0) parts.Add($"{_runCounts.Removed} removed");
            DoneSummary = $"{InstallChanges.Plural(installs, "mod")} · {string.Join(", ", parts)} · {took}";

            var downloaded = jobs.Where(j => j.NeedsDownload && !j.IsCached).Sum(j => j.BytesTotal ?? 0);
            DoneTiles =
            [
                new StatTile("DOWNLOADED", InstalledMods.FormatSize(downloaded)),
                new StatTile("FILES", ModProgressRowViewModel.Count(jobs.Sum(j => (long)j.FilesTotal))),
                new StatTile("MODS NOW", instance.Mods.Count.ToString())
            ];
        }
        else
        {
            DoneTitle = $"Removed from {instance.Name}";
            DoneSummary = $"{InstallChanges.Plural(_runCounts.Removed, "mod")} removed · {took}";
            DoneTiles =
            [
                new StatTile("REMOVED", _runCounts.Removed.ToString()),
                new StatTile("MODS NOW", instance.Mods.Count.ToString())
            ];
        }

        LaunchText = $"Launch {instance.Name}";
        SelectionNote = Wizard is not null && _source == InstallSource.Discover
            ? "Your selection in Discover was cleared."
            : "";
        Phase = InstallDialogPhase.Done;
        UpdateStepper();
    }

    private void ShowFailed(Instance instance, InstallProgressViewModel progress)
    {
        var jobs = progress.Run?.Jobs ?? [];
        var removing = jobs.Count == 0;
        var notUndone = progress.ErrorMessage.Contains("couldn't be undone");

        FailedTitle = (removing ? "Removing" : "Install") + (progress.WasCancelled ? " cancelled" : " failed");

        var updated = jobs.Where(j => j.State == ModInstallState.Done)
            .Select(j => j.Version.Identifier).Where(_installedBefore.Contains).ToList();
        FailedSummary = notUndone
            ? $"Some changes in {instance.Name} couldn't be undone; see below."
            : $"Nothing was changed in {instance.Name}." + updated.Count switch
            {
                0 => "",
                1 => $" {updated[0]} is back on its old version.",
                _ => $" {InstallChanges.JoinAll(updated)} are back on their old versions."
            };

        ErrorText = progress.ErrorMessage;
        ShowError = !progress.WasCancelled || notUndone;

        var rows = jobs.Select(FailedRowFor).OrderBy(r => r.Order).ToList();
        FailedRows = [.. rows.Take(FailedRowsShown).Select(r => r.Row)];
        FailedRowsCount = rows.Count.ToString();
        var more = rows.Skip(FailedRowsShown).ToList();
        HasMoreFailed = more.Count > 0;
        MoreFailedText = more.Count == 0 ? ""
            : $"and {more.Count} more" + (more.All(r => r.Cached) ? ", all kept in the cache" : "");

        CacheHint = CacheHintFor(jobs);
        Phase = InstallDialogPhase.Failed;
        UpdateStepper();
    }

    private (int Order, FailedRow Row, bool Cached) FailedRowFor(ModInstallJob job)
    {
        var cached = job.NeedsDownload && _installService.FindCached(job.Version) is not null;
        var keptText = cached ? "Kept in cache" : job.NeedsDownload ? "" : "Nothing to download";
        var downloaded = job.BytesTotal is > 0 ? Math.Min(1, (double)job.BytesDone / job.BytesTotal.Value) : 0;

        return job.State switch
        {
            ModInstallState.Failed => (0,
                new FailedRow(job.Version.Identifier, "Failed", Math.Max(downloaded, 0.05),
                    ShortError(job.Error), true), cached),
            ModInstallState.Done => (1, new FailedRow(job.Version.Identifier, "Rolled back", 1, keptText, false),
                cached),
            _ when cached => (2, new FailedRow(job.Version.Identifier, "Cancelled", 1, keptText, false), true),
            _ when job.BytesDone > 0 => (2,
                new FailedRow(job.Version.Identifier, "Cancelled", downloaded, "Partial download deleted", false),
                false),
            _ => (3, new FailedRow(job.Version.Identifier, "Not started", 0, "", false), !job.NeedsDownload)
        };
    }

    public static string ShortError(string? error)
    {
        if (error is null) return "";

        var lines = error.Split('\n');
        if (!lines[0].EndsWith("could not be downloaded.") || lines.Length < 2)
            return ModProgressRowViewModel.FirstLine(error);

        return lines.Length > 2
            ? "All mirrors failed"
            : string.Join(" ", lines[1].Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries).Skip(1)).Trim();
    }

    private string CacheHintFor(IReadOnlyList<ModInstallJob> jobs)
    {
        var downloads = jobs.Where(j => j.NeedsDownload).ToList();
        if (downloads.Count == 0) return "";

        var missing = downloads.Where(j => _installService.FindCached(j.Version) is null).ToList();
        if (missing.Count == 0) return "All downloads are cached, so a retry doesn't download anything.";

        var names = missing.Select(j => j.Version.Identifier).ToList();
        var size = missing.Sum(j => j.Version.DownloadSize ?? 0);
        var list = names.Count <= 3
            ? names.Count == 1 ? names[0] : $"{string.Join(", ", names.Take(names.Count - 1))} and {names[^1]}"
            : $"{string.Join(", ", names.Take(2))} and {names.Count - 2} more";
        var sizeText = size > 0 ? $" ({InstalledMods.FormatSize(size)})" : "";

        return missing.Count == downloads.Count
            ? $"Nothing was downloaded yet; a retry downloads {list}{sizeText}."
            : $"{downloads.Count - missing.Count} of {downloads.Count} downloads are cached, so a retry only " +
              $"downloads {list}{sizeText}.";
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
