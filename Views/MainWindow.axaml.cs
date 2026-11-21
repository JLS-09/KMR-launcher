using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using KMRLauncherMvvm.ViewModels;

namespace KMRLauncherMvvm.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
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
