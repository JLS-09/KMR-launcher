using Avalonia.Controls;

namespace KMRLauncherMvvm.Views;

public partial class InstancesPageView : UserControl
{
    // Below this page width the Mods tab's toolbar doesn't fit on one row (filter, filters, Update N, Add mods)
    private const double ToolbarOneRowWidth = 1220;

    // Below this page width the four stats (at least 150 px each, like the mockup) go 2 × 2
    private const double StatsOneRowWidth = 960;

    public InstancesPageView()
    {
        InitializeComponent();
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        Classes.Set("narrow", e.NewSize.Width < ToolbarOneRowWidth);
        Classes.Set("compact", e.NewSize.Width < StatsOneRowWidth);
    }
}
