using System;
using Avalonia;
using Avalonia.Controls;

namespace KMRLauncherMvvm.Views;

public partial class HomePageView : UserControl
{
    private const double ContinueOneRowWidth = 1000;

    private const double CardMinWidth = 250;

    private const double CardSpacing = 12;

    public static readonly StyledProperty<int> CardColumnsProperty =
        AvaloniaProperty.Register<HomePageView, int>(nameof(CardColumns), 1);

    public int CardColumns
    {
        get => GetValue(CardColumnsProperty);
        set => SetValue(CardColumnsProperty, value);
    }

    public HomePageView()
    {
        InitializeComponent();
        InstanceCards.SizeChanged += (_, e) => CardColumns =
            Math.Max(1, (int)((e.NewSize.Width + CardSpacing) / (CardMinWidth + CardSpacing)));
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        Classes.Set("stacked", e.NewSize.Width < ContinueOneRowWidth);
    }
}
