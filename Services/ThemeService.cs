using System;
using Avalonia;
using Avalonia.Media;

namespace KMRLauncherMvvm.Services;

// The brushes that follow the accent colour. Worked out the same way as in the mockups.
public record AccentPalette(Color Accent, Color AccentText, Color OnAccent, Color AccentSoft, Color AccentLine)
{
    public static AccentPalette From(Color accent)
    {
        var luminance = RelativeLuminance(accent);

        // A dark accent (like red) is too dark for text on the background, so text gets a lighter mix
        var text = luminance > 0.25 ? accent : MixWithWhite(accent, 0.35);
        // Text on an accent fill: dark on light accents (orange, green, blue), white on red
        var onAccent = luminance > 0.3 ? Color.Parse("#141414") : Colors.White;

        return new AccentPalette(accent, text, onAccent, WithAlpha(accent, 0x24), WithAlpha(accent, 0x73));
    }

    public static double RelativeLuminance(Color color)
    {
        static double Channel(byte value)
        {
            var v = value / 255.0;
            return v <= 0.03928 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Channel(color.R) + 0.7152 * Channel(color.G) + 0.0722 * Channel(color.B);
    }

    private static Color MixWithWhite(Color color, double amount)
    {
        static byte Mix(byte value, double amount) => (byte)Math.Round(value + (255 - value) * amount, MidpointRounding.AwayFromZero);
        return Color.FromRgb(Mix(color.R, amount), Mix(color.G, amount), Mix(color.B, amount));
    }

    private static Color WithAlpha(Color color, byte alpha) => Color.FromArgb(alpha, color.R, color.G, color.B);
}

public static class ThemeService
{
    public const string DefaultAccent = "#DF0105";

    // Red, orange, green, blue (Phase 9 decisions)
    public static readonly string[] AccentChoices = ["#DF0105", "#FF7A1A", "#3FB950", "#58A6FF"];

    // Replaces the accent brushes in App.axaml. Everything that uses them through DynamicResource updates.
    public static void ApplyAccent(string? hex)
    {
        if (Application.Current is not { } app) return;
        if (!Color.TryParse(hex, out var color)) color = Color.Parse(DefaultAccent);

        var palette = AccentPalette.From(color);
        var resources = app.Resources;

        resources["Accent"] = new SolidColorBrush(palette.Accent);
        resources["AccentText"] = new SolidColorBrush(palette.AccentText);
        resources["OnAccent"] = new SolidColorBrush(palette.OnAccent);
        resources["AccentSoft"] = new SolidColorBrush(palette.AccentSoft);
        resources["AccentLine"] = new SolidColorBrush(palette.AccentLine);

        // Fluent controls (CheckBox, ToggleSwitch, ProgressBar, …) use these
        resources["SystemAccentColor"] = palette.Accent;
        resources["SystemAccentColorDark1"] = palette.Accent;
        resources["SystemAccentColorDark2"] = palette.Accent;
        resources["SystemAccentColorDark3"] = palette.Accent;
        resources["SystemAccentColorLight1"] = palette.AccentText;
        resources["SystemAccentColorLight2"] = palette.AccentText;
        resources["SystemAccentColorLight3"] = palette.AccentText;
    }
}
