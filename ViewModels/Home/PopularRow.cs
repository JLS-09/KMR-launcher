using System.Collections.Generic;
using System.Linq;
using KMRLauncherMvvm.Models;
using KMRLauncherMvvm.ViewModels.Discover;

namespace KMRLauncherMvvm.ViewModels.Home;

public sealed record PopularRow(int Rank, Mod Mod, string Downloads)
{
    public string Name => Mod.Name;
    public string Abstract => Mod.Abstract;
    public string AddTip => $"Add {Mod.Name} to selection";

    public static List<PopularRow> For(Instance? primary, HomeData data, IReadOnlyDictionary<string, Mod> mods) =>
        primary is null || data.PopularFor != primary.RootPath
            ? []
            :
            [
                .. data.Popular
                    .Where(p => mods.ContainsKey(p.Identifier) && primary.Mods.TrueForAll(m => m.Identifier != p.Identifier))
                    .Select((p, i) => new PopularRow(i + 1, mods[p.Identifier], DiscoverFilter.FormatDownloads(p.Downloads)))
            ];
}
