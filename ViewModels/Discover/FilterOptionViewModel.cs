using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace KMRLauncherMvvm.ViewModels.Discover;

public partial class FilterOptionViewModel(string name, int count, Action changed) : ObservableObject
{
    public string Name { get; } = name;
    public string CountText { get; } = count > 0 ? count.ToString("N0") : "";

    [ObservableProperty] private bool _isChecked;

    partial void OnIsCheckedChanged(bool value) => changed();
}
