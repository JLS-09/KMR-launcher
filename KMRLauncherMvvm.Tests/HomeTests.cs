using KMRLauncherMvvm.Models;
using KMRLauncherMvvm.ViewModels;
using KMRLauncherMvvm.ViewModels.Home;
using static KMRLauncherMvvm.Tests.TestData;

namespace KMRLauncherMvvm.Tests;

public class HomeTests
{
    private static Instance Played(string name, DateTime lastPlayed, bool primary = false) =>
        new(name, "/tmp/" + name, "1.12.5") { PlayTime = 600, LastPlayed = lastPlayed, IsPrimary = primary };

    [Fact]
    public void Continue_is_the_last_played_instance_or_else_the_primary()
    {
        var primary = new Instance("Primary", "/tmp/p", "1.12.5") { IsPrimary = true };
        var older = Played("Older", DateTime.Now.AddDays(-3));
        var newer = Played("Newer", DateTime.Now.AddHours(-2));

        Assert.Same(newer, HomeContinue.Pick([primary, older, newer], primary));
        Assert.Same(primary, HomeContinue.Pick([primary, new Instance("Never", "/tmp/n", "1.12.5")], primary));
        Assert.Null(HomeContinue.Pick([], null));
    }

    [Fact]
    public void Pre_flight_counts_updates_replacements_and_mods_not_listed()
    {
        var instance = Instance("1.12.5", Version("A", "1.0"), Version("B", "1.0"), Version("C", "1.0"),
            Version("D", "1.0"));
        instance.ModRecords["C"] = new InstalledModRecord { IsUserRequested = false };
        var checks = new InstanceCheckResult
        {
            Updates = [new ModUpdateResult("A", "1.0", "1.1")],
            Replacements = [new ModReplacementResult("B", "1.0", "NewB", "2.0")],
            NotListed = ["B", "C", "Gone"]
        };

        var home = new HomeContinue(instance, checks);

        Assert.Equal("4", home.ModsCount);
        Assert.Equal("3 picked · 1 dependencies", home.ModsDetail);
        Assert.Equal("1 mod ready", home.ReadyText);
        Assert.Equal("2 updates", home.UpdatesText);
        Assert.Equal("2 not listed for 1.12.5", home.NotListedText);
        Assert.Equal(ModsFilter.Updates, home.ReviewFilter);
    }

    [Fact]
    public void Without_checks_every_mod_is_ready()
    {
        var home = new HomeContinue(Instance("1.12.5", Version("A", "1.0")), null);

        Assert.Equal("1 mod ready", home.ReadyText);
        Assert.False(home.HasUpdates);
        Assert.False(home.HasNotListed);
        Assert.Equal(ModsFilter.All, home.ReviewFilter);
    }

    [Fact]
    public void Review_opens_the_issues_when_there_are_no_updates()
    {
        var home = new HomeContinue(Instance("1.12.5", Version("A", "1.0")),
            new InstanceCheckResult { NotListed = ["A"] });

        Assert.Equal("0 mods ready", home.ReadyText);
        Assert.Equal(ModsFilter.Issues, home.ReviewFilter);
    }
}
