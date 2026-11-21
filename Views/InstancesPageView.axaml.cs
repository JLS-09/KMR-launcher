using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using KMRLauncherMvvm.ViewModels;

namespace KMRLauncherMvvm.Views;

public partial class InstancesPageView : UserControl
{
    private const double ToolbarOneRowWidth = 1220;

    private const double StatsOneRowWidth = 960;

    private const double SettingsTwoColumnWidth = 940;

    public InstancesPageView()
    {
        InitializeComponent();
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        Classes.Set("narrow", e.NewSize.Width < ToolbarOneRowWidth);
        Classes.Set("compact", e.NewSize.Width < StatsOneRowWidth);
        Classes.Set("stacked", e.NewSize.Width < SettingsTwoColumnWidth);
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

    private void CloseDeleteFlyout(object? sender, RoutedEventArgs e) => DeleteButton.Flyout?.Hide();

    private void ConfirmDelete(object? sender, RoutedEventArgs e)
    {
        DeleteButton.Flyout?.Hide();
        if (DataContext is InstancesPageViewModel { DeleteInstanceCommand: var delete } && delete.CanExecute(null))
            delete.Execute(null);
    }
}
