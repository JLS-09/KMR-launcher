using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;

namespace KMRLauncherMvvm.Controls;

public sealed record SkeletonRow(double Wide, double Narrow);

public class SkeletonList : ItemsControl
{
    private static readonly double[] WideWidths = [180, 132, 216, 158, 120, 196, 144];
    private static readonly double[] NarrowWidths = [88, 116, 70, 104, 80, 96, 62];

    public static readonly StyledProperty<int> CountProperty =
        AvaloniaProperty.Register<SkeletonList, int>(nameof(Count), 10);

    public int Count
    {
        get => GetValue(CountProperty);
        set => SetValue(CountProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(ItemsControl);

    public SkeletonList() => Fill();

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == CountProperty) Fill();
    }

    private void Fill() => ItemsSource = Enumerable.Range(0, Count)
        .Select(i => new SkeletonRow(WideWidths[i % WideWidths.Length], NarrowWidths[i % NarrowWidths.Length]))
        .ToArray();
}
