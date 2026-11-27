using Avalonia.Controls;

namespace KMRLauncherMvvm.Views;

public partial class HomePageView : UserControl
{
    private const double ContinueOneRowWidth = 1000;

    public HomePageView()
    {
        InitializeComponent();
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        Classes.Set("stacked", e.NewSize.Width < ContinueOneRowWidth);
    }
}
