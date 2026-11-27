using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using KMRLauncherMvvm.Models;
using KMRLauncherMvvm.ViewModels.Instances;

namespace KMRLauncherMvvm.ViewModels.Home;

public partial class HomeInstanceCard(Instance instance) : ObservableObject
{
    public Instance Instance { get; } = instance;
    public string Name { get; } = instance.Name;
    public bool IsPrimary { get; } = instance.IsPrimary;
    public string Version { get; } = instance.Version;
    public string ModsText { get; } = $"{instance.Mods.Count} mods";
    public string LastPlayedText { get; } = InstanceTexts.LastPlayed(instance) is "Never" ? "Never played"
        : InstanceTexts.LastPlayed(instance);
    public string LaunchTip { get; } = $"Launch {instance.Name}";

    [ObservableProperty] private string _sizeText = "–";

    public static List<HomeInstanceCard> For(IEnumerable<Instance> usable) =>
    [
        .. usable.OrderByDescending(i => i.PlayTime > 0 ? i.LastPlayed : DateTime.MinValue)
            .ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
            .Select(i => new HomeInstanceCard(i))
    ];
}
