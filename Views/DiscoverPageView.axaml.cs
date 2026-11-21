using Avalonia.Controls;

namespace KMRLauncherMvvm.Views;

public partial class DiscoverPageView : UserControl
{
    private const double WideWidth = 1070;

    public DiscoverPageView()
    {
        InitializeComponent();
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        Classes.Set("narrow", e.NewSize.Width < WideWidth);
    }
}
