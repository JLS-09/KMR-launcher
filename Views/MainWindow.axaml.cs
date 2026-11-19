using Avalonia.Controls;
using Avalonia.Input;

namespace KMRLauncherMvvm.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    // Ctrl K focuses the search box in the top bar
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
}
