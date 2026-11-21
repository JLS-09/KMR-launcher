using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using KMRLauncherMvvm.ViewModels;

namespace KMRLauncherMvvm.Views.Instances;

public partial class InstanceSettingsTabView : UserControl
{
    public InstanceSettingsTabView()
    {
        InitializeComponent();
    }

    private async void BrowsePrefix_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not InstancesPageViewModel { InstanceSettings: { } settings }) return;
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage) return;

        var folders = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Choose the Wine prefix folder",
            AllowMultiple = false
        });
        if (folders.FirstOrDefault()?.TryGetLocalPath() is { } path) settings.Prefix = path;
    }

    private void CloseDeleteFlyout(object? sender, RoutedEventArgs e) => DeleteButton.Flyout?.Hide();

    private void ConfirmDelete(object? sender, RoutedEventArgs e)
    {
        DeleteButton.Flyout?.Hide();
        if (DataContext is InstancesPageViewModel { DeleteInstanceCommand: var delete } && delete.CanExecute(null))
            delete.Execute(null);
    }
}
