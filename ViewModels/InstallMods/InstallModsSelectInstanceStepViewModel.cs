using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KMRLauncherMvvm.Models;
using KMRLauncherMvvm.Services;

namespace KMRLauncherMvvm.ViewModels.InstallMods;

public partial class InstanceCardViewModel(Instance instance, string fitText, FitKind fitKind) : ObservableObject
{
    public Instance Instance { get; } = instance;
    public string Name => Instance.Name;
    public string KspText => $"KSP {Instance.Version}";
    public string FitText { get; } = fitText;
    public bool Fits => fitKind == FitKind.Fits;
    public bool IsInstalled => fitKind == FitKind.Installed;
    public bool IsWarning => fitKind == FitKind.Warning;

    [ObservableProperty] private bool _isSelected;
}

public partial class InstallModsSelectInstanceStepViewModel : InstallModsStepViewModel
{
    [ObservableProperty] private ObservableCollection<Instance> _instances;
    [ObservableProperty] private IReadOnlyList<InstanceCardViewModel> _cards = [];
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasChanges))]
    private IReadOnlyList<ChangeRow> _changes = [];

    public bool HasChanges => Changes.Count > 0;
    public bool HasNoInstances => Instances.Count == 0;
    public Func<Task<Instance?>>? CreateInstance { get; set; }
    [ObservableProperty] private IReadOnlyList<ResolutionProblem> _problems = [];
    [ObservableProperty] private bool _hasProblems;
    [ObservableProperty] private bool _canContinue;

    [ObservableProperty] private string? _choicesHint;

    private readonly DependencyService _dependencyService;
    private bool _syncingCards;

    public event Action? Resolved;

    public string InstancesCountText => InstallChanges.Plural(Instances.Count, "instance");

    public InstallModsSelectInstanceStepViewModel(InstallModsData installModsData, DependencyService dependencyService,
        Instance? target = null)
        :
        base(installModsData)
    {
        _dependencyService = dependencyService;

        InstallModsData.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Models.InstallModsData.SelectedInstance))
                Resolve();
        };

        _instances = new ObservableCollection<Instance>(App.Settings.UsableInstances);
        BuildCards();
        InstallModsData.SelectedInstance =
            Instances.FirstOrDefault(i => i.RootPath == target?.RootPath) ?? Instances.FirstOrDefault();
    }

    private void BuildCards()
    {
        var requested = InstallModsData.RequestedModVersions.ToList();
        foreach (var card in Cards) card.PropertyChanged -= OnCardChanged;

        Cards =
        [
            .. Instances.Select(instance =>
            {
                var (text, kind) = InstallChanges.FitHint(instance, requested);
                var card = new InstanceCardViewModel(instance, text, kind);
                card.PropertyChanged += OnCardChanged;
                return card;
            })
        ];
    }

    private void OnCardChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (_syncingCards || e.PropertyName != nameof(InstanceCardViewModel.IsSelected) ||
            sender is not InstanceCardViewModel { IsSelected: true } card) return;

        InstallModsData.SelectedInstance = card.Instance;
    }

    public void Resolve()
    {
        try
        {
            _dependencyService.ResolveDependencies(InstallModsData);
        }
        catch (Exception e)
        {
            InstallModsData.Problems = [new ResolutionProblem { Mod = "", Message = $"Unexpected error: {e.Message}" }];
        }

        _syncingCards = true;
        foreach (var card in Cards) card.IsSelected = card.Instance == InstallModsData.SelectedInstance;
        _syncingCards = false;

        Changes = InstallChanges.Rows(InstallModsData, _ => false);
        Problems = [.. InstallModsData.Problems];
        HasProblems = Problems.Count > 0;
        CanContinue = InstallModsData.SelectedInstance is not null && !HasProblems;
        Resolved?.Invoke();
    }

    [RelayCommand]
    private async Task NewInstance()
    {
        if (CreateInstance is null || await CreateInstance() is not { } created) return;

        Instances = new ObservableCollection<Instance>(App.Settings.UsableInstances);
        BuildCards();
        OnPropertyChanged(nameof(HasNoInstances));
        OnPropertyChanged(nameof(InstancesCountText));
        var select = Instances.FirstOrDefault(i => i.RootPath == created.RootPath);
        if (select is not null && select != InstallModsData.SelectedInstance) InstallModsData.SelectedInstance = select;
        else Resolve();
    }

    public override void OnEntering(bool forward)
    {
        if (!forward) Resolve();
    }

    partial void OnCanContinueChanged(bool value) => OnPropertyChanged(nameof(CanGoNext));

    public override string Title => "Instance";
    public override bool CanGoNext => CanContinue;
}
