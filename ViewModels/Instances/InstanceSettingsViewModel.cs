using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KMRLauncherMvvm.Models;
using KMRLauncherMvvm.Services;

namespace KMRLauncherMvvm.ViewModels.Instances;

public partial class InstanceSettingsViewModel : ObservableObject
{
    private readonly IReadOnlyList<Instance> _instances;
    private readonly Action<Instance>? _save;
    private Snapshot _saved;

    public string RootPath { get; }
    public string FolderText { get; }
    public string DefaultPrefix { get; }
    public CompatibleVersionsViewModel Versions { get; }

    [ObservableProperty] private string _name;
    [ObservableProperty] private bool _isPrimary;
    [ObservableProperty] private string _prefix;
    [ObservableProperty] private string _launchArguments;
    [ObservableProperty] private string _changesText = "";
    [ObservableProperty] private string? _nameError;

    [ObservableProperty] [NotifyCanExecuteChangedFor(nameof(SaveCommand), nameof(RevertCommand))]
    private bool _hasChanges;

    public InstanceSettingsViewModel(Instance instance, IReadOnlyList<Instance> allInstances,
        Action<Instance>? save = null)
    {
        _instances = allInstances;
        _save = save;
        RootPath = instance.RootPath;
        FolderText = InstanceTexts.ShortPath(instance.RootPath);
        DefaultPrefix = InstanceTexts.ShortPath(WinePrefixes.DefaultFor(instance, allInstances));

        _saved = new Snapshot(instance.Name, instance.IsPrimary, Expanded(instance.PrefixPath),
            instance.LaunchArguments, [.. instance.CompatibleKspVersions]);
        _name = instance.Name;
        _isPrimary = instance.IsPrimary;
        _prefix = InstanceTexts.ShortPath(instance.PrefixPath);
        _launchArguments = instance.LaunchArguments;

        Versions = new CompatibleVersionsViewModel(instance.Version, _saved.Compatible);
        Versions.Changed += Recount;

        Recount();
    }

    partial void OnNameChanged(string value) => Recount();
    partial void OnIsPrimaryChanged(bool value) => Recount();
    partial void OnPrefixChanged(string value) => Recount();
    partial void OnLaunchArgumentsChanged(string value) => Recount();

    private string PrefixToSave => Prefix.Trim().Length == 0 ? Expanded(DefaultPrefix) : Expanded(Prefix);

    private void Recount()
    {
        var changes = 0;
        if (Name.Trim() != _saved.Name) changes++;
        if (IsPrimary != _saved.IsPrimary) changes++;
        if (PrefixToSave != _saved.Prefix) changes++;
        if (LaunchArguments.Trim() != _saved.LaunchArguments.Trim()) changes++;

        var compatible = Versions.Entries().ToHashSet();
        changes += compatible.Count(e => !_saved.Compatible.Contains(e)) +
                   _saved.Compatible.Count(e => !compatible.Contains(e));

        HasChanges = changes > 0;
        ChangesText = changes == 1 ? "1 unsaved change" : $"{changes} unsaved changes";
        NameError = Name.Trim().Length == 0 ? "The instance needs a name." : null;
        SaveCommand.NotifyCanExecuteChanged();
    }

    private bool CanSave() => HasChanges && NameError is null;

    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save()
    {
        var instance = _instances.FirstOrDefault(i => i.RootPath == RootPath);
        if (instance is not null && Apply()) _save?.Invoke(instance);
    }

    public bool Apply()
    {
        var instance = _instances.FirstOrDefault(i => i.RootPath == RootPath);
        if (instance is null || !CanSave()) return false;

        instance.Name = Name.Trim();
        if (IsPrimary)
            foreach (var other in _instances)
                other.IsPrimary = other == instance;
        else
            instance.IsPrimary = false;
        instance.PrefixPath = PrefixToSave;
        instance.LaunchArguments = LaunchArguments.Trim();
        instance.CompatibleKspVersions = Versions.Entries();

        _saved = new Snapshot(instance.Name, instance.IsPrimary, instance.PrefixPath, instance.LaunchArguments,
            [.. instance.CompatibleKspVersions]);
        Name = instance.Name;
        Prefix = InstanceTexts.ShortPath(instance.PrefixPath);
        LaunchArguments = instance.LaunchArguments;
        Recount();
        return true;
    }

    [RelayCommand(CanExecute = nameof(HasChanges))]
    private void Revert()
    {
        Name = _saved.Name;
        IsPrimary = _saved.IsPrimary;
        Prefix = InstanceTexts.ShortPath(_saved.Prefix);
        LaunchArguments = _saved.LaunchArguments;
        Versions.Reset(_saved.Compatible);
    }

    private static string Expanded(string path) => path.Trim().Length == 0 ? "" : WinePrefixes.Expand(path);

    private sealed record Snapshot(
        string Name,
        bool IsPrimary,
        string Prefix,
        string LaunchArguments,
        HashSet<string> Compatible);
}