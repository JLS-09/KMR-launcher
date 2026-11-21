using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using KMRLauncherMvvm.ViewModels.NewInstance;

namespace KMRLauncherMvvm.Views.NewInstance;

public partial class NewInstanceDialogView : UserControl
{
    public NewInstanceDialogView()
    {
        InitializeComponent();
    }

    private async void ChangeFolder_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not NewInstanceDialogViewModel vm) return;
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage) return;

        var folders = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Folder for the new instance",
            AllowMultiple = false
        });
        if (folders.FirstOrDefault()?.TryGetLocalPath() is { } path) vm.ChangeParent(path);
    }

    private async void AddZip_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not NewInstanceDialogViewModel vm) return;
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage) return;

        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Add a KSP zip",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Zip files") { Patterns = ["*.zip"] }, FilePickerFileTypes.All]
        });
        if (files.FirstOrDefault()?.TryGetLocalPath() is { } path) vm.AddZip(path);
    }
}
