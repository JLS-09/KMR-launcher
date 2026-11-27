using System.Collections.Generic;
using System.Linq;
using KMRLauncherMvvm.Models;

namespace KMRLauncherMvvm.Services;

public record ModCheck(ModVersion Installed, Mod? Mod, ModVersion? Update, ModVersion? Replacement, bool IsNotListed)
{
    public bool HasUpdate => Update is not null;
    public bool HasReplacement => Replacement is not null;
}

public static class InstanceChecks
{
    public static ModVersion? NewestFit(Mod mod, GameVersionCriteria criteria) =>
        mod.Versions.FirstOrDefault(v => KspCompatibility.IsCompatible(v, criteria));

    public static List<ModCheck> CheckAll(Instance instance, IReadOnlyDictionary<string, Mod> mods)
    {
        var criteria = GameVersionCriteria.FromInstance(instance);
        return [.. instance.Mods.Select(v => Check(instance, v, mods, criteria))];
    }

    public static ModCheck Check(Instance instance, ModVersion installed, IReadOnlyDictionary<string, Mod> mods,
        GameVersionCriteria criteria)
    {
        var mod = mods.GetValueOrDefault(installed.Identifier);
        var replacement = InstalledMods.FindReplacement(instance, installed, mods, criteria);

        ModVersion? update = null;
        if (mod is not null && replacement is null && NewestFit(mod, criteria) is { } newest &&
            CkanVersion.Compare(newest.Version, installed.Version) > 0)
            update = newest;

        var notListed = installed.Kind is null or "package" && !KspCompatibility.IsCompatible(installed, criteria);
        return new ModCheck(installed, mod, update, replacement, notListed);
    }
}
