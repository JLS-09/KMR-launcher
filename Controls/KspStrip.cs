using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using KMRLauncherMvvm.Models;
using KMRLauncherMvvm.Services;

namespace KMRLauncherMvvm.Controls;

// What the strip shows for one mod version: a cell per minor version 1.8–1.12 and a label
public record KspStripInfo(bool[] Cells, string Label)
{
    public static readonly string[] Minors = ["1.8", "1.9", "1.10", "1.11", "1.12"];

    public static KspStripInfo For(ModVersion? version) => version is null
        ? new KspStripInfo(new bool[Minors.Length], "")
        : new KspStripInfo(
            [.. Minors.Select(minor => CompatibilityService.CoversMinor(version, minor))],
            CompatibilityService.DescribeKspRange(version));
}

// Five cells for KSP 1.8–1.12 with the range under it ("1.8–1.12", "1.12.3 strict").
// Drawn directly instead of with child controls, because it is in every row of long lists.
public class KspStrip : Control
{
    public static readonly StyledProperty<ModVersion?> VersionProperty =
        AvaloniaProperty.Register<KspStrip, ModVersion?>(nameof(Version));

    public static readonly StyledProperty<IBrush?> OnBrushProperty =
        AvaloniaProperty.Register<KspStrip, IBrush?>(nameof(OnBrush));

    public static readonly StyledProperty<IBrush?> OffBrushProperty =
        AvaloniaProperty.Register<KspStrip, IBrush?>(nameof(OffBrush));

    public static readonly StyledProperty<IBrush?> LabelBrushProperty =
        AvaloniaProperty.Register<KspStrip, IBrush?>(nameof(LabelBrush));

    public static readonly StyledProperty<FontFamily> FontFamilyProperty =
        AvaloniaProperty.Register<KspStrip, FontFamily>(nameof(FontFamily), FontFamily.Default);

    private const double CellHeight = 6;
    private const double CellGap = 2;
    private const double LabelTop = CellHeight + 4;
    private const double LabelSize = 11;

    private KspStripInfo _info = KspStripInfo.For(null);

    static KspStrip()
    {
        AffectsRender<KspStrip>(VersionProperty, OnBrushProperty, OffBrushProperty, LabelBrushProperty,
            FontFamilyProperty);
        ClipToBoundsProperty.OverrideDefaultValue<KspStrip>(true);
    }

    public ModVersion? Version
    {
        get => GetValue(VersionProperty);
        set => SetValue(VersionProperty, value);
    }

    public IBrush? OnBrush
    {
        get => GetValue(OnBrushProperty);
        set => SetValue(OnBrushProperty, value);
    }

    public IBrush? OffBrush
    {
        get => GetValue(OffBrushProperty);
        set => SetValue(OffBrushProperty, value);
    }

    public IBrush? LabelBrush
    {
        get => GetValue(LabelBrushProperty);
        set => SetValue(LabelBrushProperty, value);
    }

    public FontFamily FontFamily
    {
        get => GetValue(FontFamilyProperty);
        set => SetValue(FontFamilyProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == VersionProperty) _info = KspStripInfo.For(Version);
    }

    protected override Size MeasureOverride(Size availableSize) =>
        new(double.IsInfinity(availableSize.Width) ? 104 : availableSize.Width, LabelTop + LabelSize + 4);

    public override void Render(DrawingContext context)
    {
        var count = _info.Cells.Length;
        var cellWidth = (Bounds.Width - CellGap * (count - 1)) / count;

        for (var i = 0; i < count; i++)
        {
            var brush = _info.Cells[i] ? OnBrush : OffBrush;
            if (brush is null) continue;
            context.FillRectangle(brush, new Rect(i * (cellWidth + CellGap), 0, cellWidth, CellHeight), 2);
        }

        if (_info.Label.Length == 0) return;
        var text = new FormattedText(_info.Label, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface(FontFamily), LabelSize, LabelBrush);
        context.DrawText(text, new Point(0, LabelTop));
    }
}
