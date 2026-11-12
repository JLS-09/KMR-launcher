using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using KMRLauncherMvvm.Models;

namespace KMRLauncherMvvm.ViewModels.InstallMods;

public partial class ChoiceGroupViewModel : ObservableObject
{
    public Relationship Relationship { get; }
    public ObservableCollection<ModVersionWrapper> Options { get; }

    public string Title { get; }
    public string RequiredByText { get; }
    public string? HelpText { get; }
    public bool HasHelpText { get; }

    public bool IsUnsatisfiable { get; }
    public string ErrorText { get; }

    [ObservableProperty] private ModVersionWrapper? _selectedOption;

    public ChoiceGroupViewModel(Relationship relationship, IEnumerable<ModVersionWrapper> options,
        IReadOnlyCollection<string> requiredBy)
    {
        Relationship = relationship;
        Options = new ObservableCollection<ModVersionWrapper>(options);

        var wanted = relationship.AnyOf is { Count: > 0 } anyOf
            ? $"one of {string.Join(", ", anyOf.Select(a => a.Name))}"
            : $"a mod that provides {relationship.Name}";

        Title = char.ToUpper(wanted[0]) + wanted[1..];
        RequiredByText = requiredBy.Count > 0 ? $"Needed by {string.Join(", ", requiredBy)}" : "";

        HelpText = relationship.ChoiceHelpText;
        HasHelpText = !string.IsNullOrWhiteSpace(HelpText);

        IsUnsatisfiable = Options.Count == 0;
        ErrorText = IsUnsatisfiable
            ? $"{(requiredBy.Count > 0 ? string.Join(", ", requiredBy) : "A mod")} requires {wanted}, which is not available."
            : "";
    }
}