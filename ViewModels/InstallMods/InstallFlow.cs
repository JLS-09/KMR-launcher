using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using KMRLauncherMvvm.Models;
using KMRLauncherMvvm.Services;
using KMRLauncherMvvm.Views.InstallMods;

namespace KMRLauncherMvvm.ViewModels.InstallMods;

// Opens the install wizard and then the progress window; used by Discover (install) and the Instances page
// (update, remove). Both windows become the in-window dialog in Phase 9 task 8.
public class InstallFlow(ModListService modListService, DependencyService dependencyService,
    CompatibilityService compatibilityService, InstallService installService)
{
    // Wizard → progress. Returns true when something was installed.
    public async Task<bool> InstallAsync(IReadOnlyList<PlannedModVersion> requested, Instance? target)
    {
        var viewModel = new InstallModsViewModel([.. requested], modListService, dependencyService,
            compatibilityService, target);
        var window = new InstallModsWindow { DataContext = viewModel };
        viewModel.Cancelled += () => window.Close(null);
        viewModel.Finished += data => window.Close(data);

        if (Owner() is not { } owner)
        {
            window.Show();
            return false;
        }

        var confirmed = await window.ShowDialog<InstallModsData?>(owner);
        if (confirmed?.SelectedInstance is not { } instance) return false;

        return await RunAsync(owner, InstallRun.Create(instance, confirmed));
    }

    // Removes mods without the wizard (nothing to resolve); the same rollback as an install
    public async Task<bool> RemoveAsync(Instance instance, IEnumerable<ModVersion> mods)
    {
        if (Owner() is not { } owner) return false;

        var data = new InstallModsData
        {
            SelectedInstance = instance,
            RequestedRemoveModVersions = [.. mods.Select(m => new PlannedModVersion { Version = m })]
        };
        return await RunAsync(owner, InstallRun.Create(instance, data));
    }

    private async Task<bool> RunAsync(Window owner, InstallRun run)
    {
        var progress = new InstallProgressViewModel(installService, run, App.Settings.MaxParallelDownloads);
        var progressWindow = new InstallProgressWindow { DataContext = progress };
        progress.CloseRequested += () => progressWindow.Close();

        var shown = progressWindow.ShowDialog(owner);
        var succeeded = await progress.RunAsync();
        await shown;
        return succeeded;
    }

    private static Window? Owner() =>
        (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
}
