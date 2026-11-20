using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using KMRLauncherMvvm.ViewModels;

namespace KMRLauncherMvvm.Views;

public partial class InstancesPageView : UserControl
{
    // Below this page width the Mods tab's toolbar doesn't fit on one row (filter, filters, Update N, Add mods)
    private const double ToolbarOneRowWidth = 1220;

    // Below this page width the four stats (at least 150 px each, like the mockup) go 2 × 2
    private const double StatsOneRowWidth = 960;

    // Below this page width the Settings tab puts each control under its label (220 px label column + 24 gap
    // + about 340 px for the widest controls)
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

    // Settings tab: pick a folder for the Wine prefix
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
