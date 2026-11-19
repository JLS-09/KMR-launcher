using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using KMRLauncherMvvm.Models;
using KMRLauncherMvvm.Services;

namespace KMRLauncherMvvm.Controls;

// The KSP compatibility strip in Discover's details panel: a cell per minor version 1.8–1.12 with its label.
// For a strict mod, the newest minor it supports is split into its patch versions (and is twice as wide).
// The cell of the "Check against" instance's version is green when the mod supports it.
public class KspDetailStrip : Control
{
    // Patch releases per minor version (1.8.0–1.8.1, …, 1.12.0–1.12.5)
    private static readonly (string Minor, int Patches)[] Minors =
        [("1.8", 2), ("1.9", 2), ("1.10", 2), ("1.11", 3), ("1.12", 6)];

    public static readonly StyledProperty<ModVersion?> VersionProperty =
        AvaloniaProperty.Register<KspDetailStrip, ModVersion?>(nameof(Version));

    public static readonly StyledProperty<string?> InstanceVersionProperty =
        AvaloniaProperty.Register<KspDetailStrip, string?>(nameof(InstanceVersion));

    public static readonly StyledProperty<IBrush?> OnBrushProperty =
        AvaloniaProperty.Register<KspDetailStrip, IBrush?>(nameof(OnBrush));

    public static readonly StyledProperty<IBrush?> OffBrushProperty =
        AvaloniaProperty.Register<KspDetailStrip, IBrush?>(nameof(OffBrush));

    public static readonly StyledProperty<IBrush?> FitsBrushProperty =
        AvaloniaProperty.Register<KspDetailStrip, IBrush?>(nameof(FitsBrush));

    public static readonly StyledProperty<IBrush?> LabelBrushProperty =
        AvaloniaProperty.Register<KspDetailStrip, IBrush?>(nameof(LabelBrush));

    public static readonly StyledProperty<FontFamily> FontFamilyProperty =
        AvaloniaProperty.Register<KspDetailStrip, FontFamily>(nameof(FontFamily), FontFamily.Default);

    private const double CellHeight = 10;
    private const double ColumnGap = 4;
    private const double PatchGap = 2;
    private const double LabelTop = CellHeight + 6;
    private const double LabelLineHeight = 16;

    static KspDetailStrip()
    {
        AffectsRender<KspDetailStrip>(VersionProperty, InstanceVersionProperty, OnBrushProperty, OffBrushProperty,
            FitsBrushProperty, LabelBrushProperty, FontFamilyProperty);
    }

    public ModVersion? Version
    {
        get => GetValue(VersionProperty);
        set => SetValue(VersionProperty, value);
    }

    public string? InstanceVersion
    {
        get => GetValue(InstanceVersionProperty);
        set => SetValue(InstanceVersionProperty, value);
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

    public IBrush? FitsBrush
    {
        get => GetValue(FitsBrushProperty);
        set => SetValue(FitsBrushProperty, value);
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

    protected override Size MeasureOverride(Size availableSize) =>
        new(double.IsInfinity(availableSize.Width) ? 300 : availableSize.Width, LabelTop + LabelLineHeight);

    public override void Render(DrawingContext context)
    {
        var version = Version;
        if (version is null) return;

        var instance = InstanceVersion is null ? null : KspVersions.ToFullVersion(InstanceVersion);
        var covered = Minors.Select(m => CompatibilityService.CoversMinor(version, m.Minor)).ToArray();
        var expanded = version.KspVersionStrict ? System.Array.LastIndexOf(covered, true) : -1;

        var weights = Minors.Select((_, i) => i == expanded ? 2.0 : 1.0).ToArray();
        var unit = (Bounds.Width - ColumnGap * (Minors.Length - 1)) / weights.Sum();
        var x = 0.0;

        for (var i = 0; i < Minors.Length; i++)
        {
            var width = unit * weights[i];
            var (minor, patches) = Minors[i];

            if (i == expanded)
            {
                var patchWidth = (width - PatchGap * (patches - 1)) / patches;
                for (var p = 0; p < patches; p++)
                {
                    var patch = $"{minor}.{p}";
                    var on = CompatibilityService.CoversVersion(version, patch);
                    var brush = on ? patch == instance ? FitsBrush : OnBrush : OffBrush;
                    Fill(context, brush, new Rect(x + p * (patchWidth + PatchGap), 0, patchWidth, CellHeight));
                }

                var first = $"{minor}.0";
                var last = $"{minor}.{patches - 1}";
                DrawLabel(context, first, x, first == instance, alignRight: false);
                DrawLabel(context, last, x + width, last == instance, alignRight: true);
            }
            else
            {
                var fits = covered[i] && instance is not null && KspVersions.ToMinorPrefix(instance) == minor &&
                           CompatibilityService.CoversVersion(version, instance);
                Fill(context, covered[i] ? fits ? FitsBrush : OnBrush : OffBrush, new Rect(x, 0, width, CellHeight));
                DrawLabel(context, minor, x, fits, alignRight: false);
            }

            x += width + ColumnGap;
        }
    }

    private static void Fill(DrawingContext context, IBrush? brush, Rect rect)
    {
        if (brush is not null) context.FillRectangle(brush, rect, 2);
    }

    private void DrawLabel(DrawingContext context, string text, double x, bool fits, bool alignRight)
    {
        var formatted = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface(FontFamily), 11, fits ? FitsBrush : LabelBrush);
        var left = alignRight ? x - formatted.Width : x;
        context.DrawText(formatted, new Point(left, LabelTop + (LabelLineHeight - formatted.Height) / 2));
    }
}
