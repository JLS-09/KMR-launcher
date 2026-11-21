using Avalonia.Controls;

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
}
