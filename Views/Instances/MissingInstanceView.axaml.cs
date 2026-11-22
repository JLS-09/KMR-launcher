using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using KMRLauncherMvvm.ViewModels;

namespace KMRLauncherMvvm.Views.Instances;

public partial class MissingInstanceView : UserControl
{
    public MissingInstanceView()
    {
        InitializeComponent();
    }

    private async void LocateFolder_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not InstancesPageViewModel vm) return;
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage) return;

        var folders = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Locate the instance folder",
            AllowMultiple = false
        });
        if (folders.FirstOrDefault()?.TryGetLocalPath() is { } path) vm.LocateFolder(path);
    }
}
