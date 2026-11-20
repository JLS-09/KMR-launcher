using Avalonia;
using Avalonia.Controls;

namespace KMRLauncherMvvm.Views;

public partial class DiscoverPageView : UserControl
{
    // Filters (210) + the list's minimum width (520) + the details panel (340)
    private const double WideWidth = 1070;

    public DiscoverPageView()
    {
        InitializeComponent();
    }

    // Below WideWidth the details panel lies over the list (the ":is(UserControl).narrow" style in the XAML)
    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        Classes.Set("narrow", e.NewSize.Width < WideWidth);
    }
}
