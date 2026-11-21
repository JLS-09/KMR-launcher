using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KMRLauncherMvvm.Models;
using KMRLauncherMvvm.Services;

namespace KMRLauncherMvvm.ViewModels.InstallMods;

public class InstallFlow(ModListService modListService, DependencyService dependencyService,
    CompatibilityService compatibilityService, InstallService installService, GameSessionService session,
    NavigationService navigation, DialogService dialogs)
{
    public Task<bool> InstallAsync(IReadOnlyList<PlannedModVersion> requested, Instance? target,
        InstallSource source = InstallSource.Discover, IReadOnlyList<ModVersion>? replaced = null)
    {
        if (dialogs.IsOpen) return Task.FromResult(false);

        var dialog = new InstallDialogViewModel(requested, target, source, modListService, dependencyService,
            compatibilityService, installService, session, navigation, dialogs, replaced);
        dialogs.Show(dialog);
        return dialog.Result;
    }

    public Task<bool> RemoveAsync(Instance instance, IEnumerable<ModVersion> mods)
    {
        if (dialogs.IsOpen) return Task.FromResult(false);

        var dialog = new InstallDialogViewModel(instance, [.. mods], modListService, dependencyService,
            compatibilityService, installService, session, navigation, dialogs);
        dialogs.Show(dialog);
        return dialog.Result;
    }
}
