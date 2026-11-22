using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using KMRLauncherMvvm.ViewModels;
using KMRLauncherMvvm.Views.InstallMods;

namespace KMRLauncherMvvm.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    protected override async void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        if (DataContext is not MainWindowViewModel viewModel) return;

        await viewModel.WarmUpAsync();
        await Dispatcher.UIThread.InvokeAsync(WarmUpDialogViews, DispatcherPriority.ApplicationIdle);
    }

    private static void WarmUpDialogViews()
    {
        _ = new InstallDialogView();
        _ = new InstallModsSelectInstanceStepView();
        _ = new InstallModsRecommendsStepView();
        _ = new InstallModsModulesStepView();
        _ = new InstallModsSummaryStepView();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.K && e.KeyModifiers == KeyModifiers.Control && SearchBox.IsEnabled)
        {
            SearchBox.Focus();
            SearchBox.SelectAll();
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }

    private void CloseStopFlyout(object? sender, RoutedEventArgs e) => StopButton.Flyout?.Hide();

    private void ConfirmStop(object? sender, RoutedEventArgs e)
    {
        StopButton.Flyout?.Hide();
        if (DataContext is MainWindowViewModel viewModel) viewModel.StopGameCommand.Execute(null);
    }
}
