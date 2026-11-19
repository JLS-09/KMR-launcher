using KMRLauncherMvvm.Controls;
using KMRLauncherMvvm.Models;
using KMRLauncherMvvm.Services;
using KMRLauncherMvvm.ViewModels.Discover;
using static KMRLauncherMvvm.Tests.TestData;

namespace KMRLauncherMvvm.Tests;

public class DiscoverTests
{
    private static (ModFit fit, Instance instance) FitFor(Mod mod, params ModVersion[] installed)
    {
        var instance = Instance("1.12.5", installed);
        return (ModFit.For(mod, instance, GameVersionCriteria.FromInstance(instance)), instance);
    }

    [Fact]
    public void Status_without_an_instance_is_empty()
    {
        var mod = Mod(Version("A", "1.0"));
        Assert.Same(ModFit.None, ModFit.For(mod, null, null));
    }

    [Fact]
    public void Newest_compatible_version_fits()
    {
        var mod = Mod(Version("A", "2.0", ksp: "1.13"), Version("A", "1.5", ksp: "1.12"), Version("A", "1.0", ksp: "1.12"));
        var (fit, _) = FitFor(mod);

        Assert.Equal(ModStatusKind.Fits, fit.Kind);
        Assert.Equal("1.5", fit.NewestFit!.Version);
    }

    [Fact]
    public void Mod_without_a_compatible_version_gets_a_warning_with_the_instance_version()
    {
        var (fit, _) = FitFor(Mod(Version("A", "1.0", ksp: "1.7.3")));

        Assert.Equal(ModStatusKind.Warning, fit.Kind);
        Assert.Equal("Not 1.12.5", fit.Text);
        Assert.False(fit.IsCompatible);
    }

    [Fact]
    public void Installed_mod_is_an_update_when_a_newer_version_fits()
    {
        var newer = Version("A", "1.10");
        var older = Version("A", "1.9");
        var mod = Mod(newer, older);

        Assert.Equal(ModStatusKind.Update, FitFor(mod, older).fit.Kind);
        Assert.Equal(ModStatusKind.Installed, FitFor(mod, newer).fit.Kind);
    }

    [Fact]
    public void Installed_mod_stays_installed_when_the_newer_version_doesnt_fit()
    {
        var installed = Version("A", "1.0");
        var mod = Mod(Version("A", "2.0", ksp: "1.13"), installed);

        Assert.Equal(ModStatusKind.Installed, FitFor(mod, installed).fit.Kind);
    }

    private static Mod Named(string id, string name, string[] authors, string[]? tags = null, string? kind = null)
    {
        var version = Version(id, "1.0");
        version.Kind = kind;
        var mod = Mod(version);
        mod.Name = name;
        mod.Author = [.. authors];
        mod.Tags = tags?.ToList();
        return mod;
    }

    [Theory]
    [InlineData("kop", true)]
    [InlineData("Planetary", true)]
    [InlineData("bryce", true)]
    [InlineData("scatterer", false)]
    public void Search_matches_name_identifier_or_author(string search, bool matches)
    {
        var mod = Named("Kopernicus", "Kopernicus Planetary System Modifier", ["BryceSchroeder"]);
        Assert.Equal(matches, DiscoverFilter.Matches(mod, ModFit.None, search, [], [], false, false));
    }

    [Fact]
    public void Tags_match_any_ticked_tag()
    {
        var mod = Named("A", "A", [], tags: ["config", "plugin"]);

        Assert.True(DiscoverFilter.Matches(mod, ModFit.None, "", ["graphics", "plugin"], [], false, false));
        Assert.False(DiscoverFilter.Matches(mod, ModFit.None, "", ["graphics"], [], false, false));
        Assert.False(DiscoverFilter.Matches(Named("B", "B", []), ModFit.None, "", ["graphics"], [], false, false));
    }

    [Fact]
    public void Kind_filter_uses_the_newest_version()
    {
        var package = Named("A", "A", []);
        var meta = Named("B", "B", [], kind: "metapackage");
        string[] metaOnly = [DiscoverFilter.MetapackageKind];

        Assert.False(DiscoverFilter.Matches(package, ModFit.None, "", [], metaOnly, false, false));
        Assert.True(DiscoverFilter.Matches(meta, ModFit.None, "", [], metaOnly, false, false));
    }

    [Fact]
    public void Only_compatible_and_hide_installed_need_an_instance()
    {
        var mod = Mod(Version("A", "1.0", ksp: "1.7"));
        var (fit, _) = FitFor(mod);

        Assert.False(DiscoverFilter.Matches(mod, fit, "", [], [], onlyCompatible: true, hideInstalled: false));
        Assert.True(DiscoverFilter.Matches(mod, ModFit.None, "", [], [], onlyCompatible: true, hideInstalled: false));

        var installed = Version("B", "1.0");
        var (installedFit, _) = FitFor(Mod(installed), installed);
        Assert.False(DiscoverFilter.Matches(Mod(installed), installedFit, "", [], [], false, hideInstalled: true));
    }

    [Fact]
    public void Needed_by_counts_each_mod_once_including_any_of_options()
    {
        var a = Mod(Version("A", "1.0", depends: [Rel("Lib"), Rel("Lib", min: "1.0")]));
        var b = Mod(Version("B", "1.0", depends: [AnyOf(Entry("Lib"), Entry("Other"))]));
        var self = Mod(Version("Lib", "1.0", depends: [Rel("Lib")]));
        var oldOnly = Mod(Version("C", "2.0"), Version("C", "1.0", depends: [Rel("Lib")]));

        var counts = DiscoverFilter.CountNeededBy([a, b, self, oldOnly]);

        Assert.Equal(2, counts["Lib"]);
        Assert.Equal(1, counts["Other"]);
    }

    [Theory]
    [InlineData(2_147_000L, "2.1M")]
    [InlineData(980_400L, "980K")]
    [InlineData(1_500L, "2K")]
    [InlineData(640L, "640")]
    [InlineData(null, "–")]
    public void Download_counts_are_shortened(long? count, string text) =>
        Assert.Equal(text, DiscoverFilter.FormatDownloads(count));

    [Fact]
    public void Authors_are_shortened()
    {
        Assert.Equal("sarbian, Blowfish", DiscoverFilter.ShortAuthors(["sarbian", "Blowfish"]));
        Assert.Equal("R-T-B +6", DiscoverFilter.ShortAuthors(["R-T-B", "a", "b", "c", "d", "e", "f"]));
        Assert.Equal("by R-T-B and 6 others", DiscoverFilter.LongAuthors(["R-T-B", "a", "b", "c", "d", "e", "f"]));
        Assert.Equal("by A and B", DiscoverFilter.LongAuthors(["A", "B"]));
    }

    [Theory]
    [InlineData("1.12", null, null, "1.12")]
    [InlineData(null, "1.12.0", "1.12.5", "1.12.0–1.12.5")]
    [InlineData(null, "1.8", null, "1.8+")]
    [InlineData(null, null, null, "any")]
    public void Exact_ksp_range_for_the_version_list(string? ksp, string? min, string? max, string text) =>
        Assert.Equal(text, CompatibilityService.DescribeKspRangeExact(Version("A", "1.0", ksp, min, max)));

    [Fact]
    public void Covers_version_checks_one_patch()
    {
        var mod = Version("A", "1.0", ksp: null, kspMin: "1.12.2", kspMax: "1.12.5", strict: true);

        Assert.False(CompatibilityService.CoversVersion(mod, "1.12.1"));
        Assert.True(CompatibilityService.CoversVersion(mod, "1.12.2"));
        Assert.True(CompatibilityService.CoversVersion(mod, "1.12.5"));
    }

    [Fact]
    public void Compatible_summary_joins_whole_minor_runs()
    {
        var instance = Instance("1.12.5");
        instance.CompatibleKspVersions = ["1.8", "1.9", "1.10", "1.11", "1.12"];
        Assert.Equal("1.12.5 · 1.8.x–1.12.x", KspVersions.DescribeCompatible(instance));

        instance.CompatibleKspVersions = ["1.12", "1.10", "1.12.3"];
        Assert.Equal("1.12.5 · 1.12.3, 1.10.x, 1.12.x", KspVersions.DescribeCompatible(instance));

        instance.CompatibleKspVersions = [];
        Assert.Equal("1.12.5", KspVersions.DescribeCompatible(instance));
    }

    [Fact]
    public void Ticking_a_row_selects_the_newest_version_that_fits()
    {
        var mod = Mod(Version("A", "2.0", ksp: "1.13"), Version("A", "1.5"));
        var row = new ModListItemViewModel(mod) { Fit = FitFor(mod).fit };

        row.IsSelected = true;

        Assert.Equal("1.5", row.SelectedVersion.Version);
    }

    [Fact]
    public void Version_list_selects_and_deselects_like_ckan()
    {
        var mod = Mod(Version("A", "2.0"), Version("A", "1.0"));
        var (fit, instance) = FitFor(mod);
        var row = new ModListItemViewModel(mod) { Fit = fit };
        var details = new ModDetailsViewModel(row, instance, GameVersionCriteria.FromInstance(instance), 0);

        details.Versions[1].IsChecked = true;
        Assert.True(row.IsSelected);
        Assert.Equal("1.0", row.SelectedVersion.Version);
        Assert.False(details.Versions[0].IsChecked);

        details.Versions[0].IsChecked = true;
        Assert.Equal("2.0", row.SelectedVersion.Version);
        Assert.False(details.Versions[1].IsChecked);

        details.Versions[0].IsChecked = false;
        Assert.False(row.IsSelected);
        Assert.True(details.Versions[0].IsNewestFit);
    }
}
