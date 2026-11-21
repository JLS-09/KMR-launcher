using System;
using Avalonia;
using Avalonia.Media;

namespace KMRLauncherMvvm.Services;

public record AccentPalette(Color Accent, Color AccentText, Color OnAccent, Color AccentSoft, Color AccentLine)
{
    public static AccentPalette From(Color accent)
    {
        var luminance = RelativeLuminance(accent);

        var text = luminance > 0.25 ? accent : MixWithWhite(accent, 0.35);
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
        static byte Mix(byte value, double amount) =>
            (byte)Math.Round(value + (255 - value) * amount, MidpointRounding.AwayFromZero);

        return Color.FromRgb(Mix(color.R, amount), Mix(color.G, amount), Mix(color.B, amount));
    }

    private static Color WithAlpha(Color color, byte alpha) => Color.FromArgb(alpha, color.R, color.G, color.B);
}

public static class ThemeService
{
    public const string DefaultAccent = "#DF0105";

    public static readonly (string Name, string Hex)[] AccentChoices =
        [("Red", "#DF0105"), ("Orange", "#FF7A1A"), ("Green", "#3FB950"), ("Blue", "#58A6FF")];

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

        resources["SystemAccentColor"] = palette.Accent;
        resources["SystemAccentColorDark1"] = palette.Accent;
        resources["SystemAccentColorDark2"] = palette.Accent;
        resources["SystemAccentColorDark3"] = palette.Accent;
        resources["SystemAccentColorLight1"] = palette.AccentText;
        resources["SystemAccentColorLight2"] = palette.AccentText;
        resources["SystemAccentColorLight3"] = palette.AccentText;
    }
}