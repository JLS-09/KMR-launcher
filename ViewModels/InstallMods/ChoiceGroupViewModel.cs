using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using KMRLauncherMvvm.Models;

namespace KMRLauncherMvvm.ViewModels.InstallMods;

public partial class ChoiceGroupViewModel : ObservableObject
{
    public Relationship Relationship { get; }
    public ObservableCollection<ChoiceOptionViewModel> Options { get; }

    public string Title { get; }

    public bool IsVirtual { get; }

    public string WantedText { get; }
    public IReadOnlyList<string> Requirers { get; }
    public string RequiredByText { get; }
    public string? HelpText { get; }
    public bool HasHelpText { get; }

    public bool IsUnsatisfiable { get; }
    public string ErrorText { get; }

    [ObservableProperty] private ChoiceOptionViewModel? _selectedOption;

    [ObservableProperty] private PlannedModVersion? _selectedPlan;

    public ChoiceGroupViewModel(Relationship relationship, IEnumerable<ChoiceOptionViewModel> options,
        IReadOnlyCollection<string> requiredBy, IReadOnlyList<string>? requirers = null)
    {
        Requirers = requirers ?? [];
        Relationship = relationship;
        Options = new ObservableCollection<ChoiceOptionViewModel>(options);

        foreach (var option in Options)
            option.PropertyChanged += OnOptionChanged;

        var wanted = relationship.AnyOf is { Count: > 0 } anyOf
            ? $"one of {string.Join(", ", anyOf.Select(a => a.Name))}"
            : $"a mod that provides {relationship.Name}";

        WantedText = wanted;
        IsVirtual = relationship.AnyOf is not { Count: > 0 };
        Title = IsVirtual ? relationship.Name ?? wanted : char.ToUpper(wanted[0]) + wanted[1..];
        RequiredByText = requiredBy.Count > 0 ? $"Needed by {InstallChanges.JoinNames([.. requiredBy])}" : "";

        HelpText = relationship.ChoiceHelpText;
        HasHelpText = !string.IsNullOrWhiteSpace(HelpText);

        IsUnsatisfiable = Options.Count == 0;
        ErrorText = IsUnsatisfiable
            ? $"{(requiredBy.Count > 0 ? string.Join(", ", requiredBy) : "A mod")} requires {wanted}, which is not available."
            : "";
    }

    partial void OnSelectedOptionChanged(ChoiceOptionViewModel? value)
    {
        foreach (var option in Options) option.IsSelected = option == value;
        UpdateSelectedPlan();
    }

    private void OnOptionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ChoiceOptionViewModel.IsSelected) &&
            sender is ChoiceOptionViewModel { IsSelected: true } picked)
        {
            SelectedOption = picked;
            return;
        }

        if (e.PropertyName != nameof(ChoiceOptionViewModel.SelectedVersion) ||
            sender is not ChoiceOptionViewModel option) return;

        if (option != SelectedOption)
            SelectedOption = option;
        else
            UpdateSelectedPlan();
    }

    private void UpdateSelectedPlan() => SelectedPlan = SelectedOption?.ToPlanned();
}