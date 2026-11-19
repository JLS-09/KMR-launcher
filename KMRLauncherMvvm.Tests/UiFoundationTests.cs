using Avalonia.Media;
using KMRLauncherMvvm.Controls;
using KMRLauncherMvvm.Services;
using static KMRLauncherMvvm.Tests.TestData;

namespace KMRLauncherMvvm.Tests;

public class UiFoundationTests
{
    [Fact]
    public void Red_accent_gets_lighter_text_and_white_on_accent()
    {
        var palette = AccentPalette.From(Color.Parse("#DF0105"));

        Assert.Equal(Color.Parse("#EA5A5D"), palette.AccentText);
        Assert.Equal(Colors.White, palette.OnAccent);
        Assert.Equal(Color.Parse("#24DF0105"), palette.AccentSoft);
        Assert.Equal(Color.Parse("#73DF0105"), palette.AccentLine);
    }

    [Theory]
    [InlineData("#FF7A1A")]
    [InlineData("#3FB950")]
    [InlineData("#58A6FF")]
    public void Light_accents_are_used_as_text_and_get_dark_text_on_top(string hex)
    {
        var accent = Color.Parse(hex);
        var palette = AccentPalette.From(accent);

        Assert.Equal(accent, palette.AccentText);
        Assert.Equal(Color.Parse("#141414"), palette.OnAccent);
    }

    [Fact]
    public void Text_on_every_accent_fill_is_readable()
    {
        foreach (var hex in ThemeService.AccentChoices)
        {
            var palette = AccentPalette.From(Color.Parse(hex));
            var a = AccentPalette.RelativeLuminance(palette.Accent);
            var b = AccentPalette.RelativeLuminance(palette.OnAccent);
            var contrast = (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);

            Assert.True(contrast >= 4.5, $"{hex}: {contrast:0.00}");
        }
    }

    [Theory]
    [InlineData("1.12", null, null, false, "00001", "1.12")]
    [InlineData("1.12.3", null, null, true, "00001", "1.12.3 strict")]
    [InlineData("any", null, null, false, "11111", "any")]
    [InlineData(null, null, null, false, "11111", "any")]
    [InlineData(null, "1.8.0", "1.12.5", false, "11111", "1.8–1.12")]
    [InlineData(null, "1.10", null, false, "00111", "1.10+")]
    [InlineData(null, null, "1.9.1", false, "11000", "≤ 1.9")]
    [InlineData(null, "1.11.0", "1.11.2", false, "00010", "1.11")]
    [InlineData("1.7.3", null, null, false, "00000", "1.7")]
    public void Ksp_strip_cells_and_label(string? ksp, string? min, string? max, bool strict, string cells,
        string label)
    {
        var info = KspStripInfo.For(Version("Mod", "1.0", ksp, min, max, strict));

        Assert.Equal(cells, string.Concat(info.Cells.Select(c => c ? '1' : '0')));
        Assert.Equal(label, info.Label);
    }
}
