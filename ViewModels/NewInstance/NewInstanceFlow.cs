using System.Threading.Tasks;
using KMRLauncherMvvm.Data;
using KMRLauncherMvvm.Models;
using KMRLauncherMvvm.Services;

namespace KMRLauncherMvvm.ViewModels.NewInstance;

public class NewInstanceFlow(ZipService zips, InstanceCreator creator, DialogService dialogs,
    NavigationService navigation)
{
    public Task<Instance?> CreateAsync()
    {
        if (dialogs.IsOpen) return Task.FromResult<Instance?>(null);

        var done = new TaskCompletionSource<Instance?>();
        NewInstanceDialogViewModel? dialog = null;
        dialog = new NewInstanceDialogViewModel(App.Settings, zips, creator, instance =>
        {
            dialogs.Close(dialog!);
            if (instance is not null) navigation.NavigateTo(ApplicationPageNames.Instances, instance);
            done.TrySetResult(instance);
        });
        dialogs.Show(dialog);
        return done.Task;
    }
}
