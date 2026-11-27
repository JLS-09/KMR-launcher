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

    [Fact]
    public void Instance_cards_are_ordered_by_last_played()
    {
        var never = new Instance("Vanilla", "/tmp/v", "1.12.5");
        var older = Played("Older", DateTime.Now.AddDays(-3), primary: true);
        var newer = Played("Newer", DateTime.Now.AddHours(-2));
        newer.Mods = [Version("A", "1.0"), Version("B", "1.0")];

        var cards = HomeInstanceCard.For([never, older, newer]);

        Assert.Equal(["Newer", "Older", "Vanilla"], cards.Select(c => c.Name));
        Assert.Equal("2 mods", cards[0].ModsText);
        Assert.True(cards[1].IsPrimary);
        Assert.Equal("Never played", cards[2].LastPlayedText);
        Assert.Equal("Launch Vanilla", cards[2].LaunchTip);
        Assert.Equal("–", cards[2].SizeText);
    }

    [Fact]
    public void Needs_attention_sums_the_checks_across_instances()
    {
        var rp1 = Played("RP-1", DateTime.Now.AddHours(-2));
        var visual = Played("Visual", DateTime.Now.AddDays(-1));
        var old = new Instance("Old", "/tmp/Old", "1.8.1");
        var data = new HomeData
        {
            Instances =
            [
                new InstanceCheckResult
                {
                    Folder = rp1.RootPath, Updates = [new("A", "1", "2"), new("B", "1", "2")], NotListed = ["KJR"],
                    Replacements = [new("C", "1", "D", "1")]
                },
                new InstanceCheckResult { Folder = visual.RootPath, Updates = [new("A", "1", "2")] },
                new InstanceCheckResult { Folder = "/tmp/forgotten", Updates = [new("Z", "1", "2")] }
            ],
            UnusedDownloads = 41,
            UnusedDownloadBytes = 1_181_116_006
        };

        var rows = HomeAttention.Rows([rp1, visual, old], data);

        Assert.Equal([AttentionKind.Updates, AttentionKind.NotListed, AttentionKind.Replacements,
            AttentionKind.UnusedDownloads], rows.Select(r => r.Kind));
        Assert.Equal(("3 mod updates", "In RP-1 and Visual"), (rows[0].Title, rows[0].Detail));
        Assert.Same(rp1, rows[0].Target);
        Assert.Equal(ModsFilter.Updates, rows[0].Filter);
        Assert.Equal(("1 mod not listed for KSP 1.12.5", "KJR in RP-1"), (rows[1].Title, rows[1].Detail));
        Assert.Equal(ModsFilter.Issues, rows[1].Filter);
        Assert.Equal("1 mod has a replacement", rows[2].Title);
        Assert.Equal(("1.1 GB of unused downloads", "41 cached archives no instance uses", "Clean up"),
            (rows[3].Title, rows[3].Detail, rows[3].ButtonText));
        Assert.Null(rows[3].Target);
    }

    [Fact]
    public void Not_listed_mods_in_several_instances_name_the_instances()
    {
        var a = Played("A", DateTime.Now.AddHours(-1));
        var b = new Instance("B", "/tmp/B", "1.12.3");
        var c = new Instance("C", "/tmp/C", "1.12.5");
        var d = new Instance("D", "/tmp/D", "1.12.5");
        HomeData Data(params Instance[] instances) => new()
        {
            Instances = [.. instances.Select(i => new InstanceCheckResult { Folder = i.RootPath, NotListed = ["X", "Y", "Z"] })]
        };

        var two = HomeAttention.Rows([a, b], Data(a, b)).Single();
        var four = HomeAttention.Rows([a, b, c, d], Data(a, b, c, d)).Single();
        var one = HomeAttention.Rows([a], Data(a)).Single();

        Assert.Equal(("6 mods not listed for their KSP versions", "In A and B"), (two.Title, two.Detail));
        Assert.Equal("In A, B and 2 more", four.Detail);
        Assert.Equal("X and 2 more in A", one.Detail);
    }

    [Fact]
    public void Nothing_needs_attention_without_checks_or_unused_downloads() =>
        Assert.Empty(HomeAttention.Rows([Played("A", DateTime.Now)], new HomeData()));
}
