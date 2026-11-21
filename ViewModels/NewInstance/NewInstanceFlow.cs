using System.Threading.Tasks;
using KMRLauncherMvvm.Data;
using KMRLauncherMvvm.Models;
using KMRLauncherMvvm.Services;

namespace KMRLauncherMvvm.ViewModels.NewInstance;

public class NewInstanceFlow(ZipService zips, InstanceCreator creator, DialogService dialogs,
    NavigationService navigation, ModListService modList, InstanceService instances)
{
    public Task<Instance?> CreateAsync(bool existingFolder = false)
    {
        if (dialogs.IsOpen) return Task.FromResult<Instance?>(null);

        return Run(openInInstances: true, existingFolder);
    }

    public Task<Instance?> CreateOverAsync() => Run(openInInstances: false, existingFolder: false);

    private Task<Instance?> Run(bool openInInstances, bool existingFolder)
    {
        var done = new TaskCompletionSource<Instance?>();
        NewInstanceDialogViewModel? dialog = null;
        dialog = new NewInstanceDialogViewModel(instances.Settings, zips, creator, instance =>
        {
            dialogs.Close(dialog!);
            if (instance is not null && openInInstances)
                navigation.NavigateTo(ApplicationPageNames.Instances, instance);
            done.TrySetResult(instance);
        }, () => modList.Mods, () => instances.Save()) { IsFolderTab = existingFolder };
        dialogs.ShowOver(dialog);
        return done.Task;
    }
}
