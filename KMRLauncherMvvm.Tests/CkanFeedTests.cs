using KMRLauncherMvvm.Models;
using KMRLauncherMvvm.Services;
using KMRLauncherMvvm.ViewModels.Home;
using LibGit2Sharp;
using static KMRLauncherMvvm.Tests.TestData;

namespace KMRLauncherMvvm.Tests;

public class CkanFeedTests : TempFolderTest
{
    private static readonly DateTimeOffset Now = new(2026, 11, 27, 12, 0, 0, TimeSpan.FromHours(1));

    private string Commit(Repository repo, params string[] files)
    {
        foreach (var file in files)
        {
            var path = Path.Combine(Temp, file);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, Guid.NewGuid().ToString());
            Commands.Stage(repo, file);
        }

        var signature = new Signature("test", "test@example.com", Now);
        return repo.Commit("update", signature, signature).Sha;
    }

    [Fact]
    public void The_diff_finds_new_versions_and_new_mods()
    {
        Repository.Init(Temp);
        string from, to;
        using (var repo = new Repository(Temp))
        {
            from = Commit(repo, "A/A-1.0.ckan", "C/readme.txt", "download_counts.json");
            to = Commit(repo, "A/A-1.0.ckan", "A/A-1.1.ckan", "B/B-1.0.ckan", "B/B-1.1.ckan", "C/C-1.0.ckan",
                "download_counts.json", "D/notes.txt");
        }

        var diff = GitHelper.DiffCkanMeta(Temp, from, to);

        Assert.NotNull(diff);
        Assert.Equal([new CkanFile("A", "A-1.1")], diff.Value.NewVersions);
        Assert.Equal(["B-1.0", "B-1.1", "C-1.0"], diff.Value.NewMods.Select(f => f.Id).Order());
        Assert.Null(GitHelper.DiffCkanMeta(Temp, from, new string('0', 40)));
    }

    private static ModVersion Released(string id, string version, int hoursAgo)
    {
        var v = Version(id, version);
        v.ReleaseDate = Now.UtcDateTime.AddHours(-hoursAgo);
        return v;
    }

    [Fact]
    public void The_feed_lists_your_updates_all_updates_and_new_mods()
    {
        var installedA = Version("A", "1.0");
        var rp1 = new Instance("RP-1 Express", "/tmp/rp1", "1.12.5") { Mods = [installedA, Version("B", "2.0")] };
        var visual = new Instance("Visual Overhaul", "/tmp/visual", "1.12.5") { Mods = [Version("A", "1.0")] };
        var mods = new[]
        {
            Mod(Released("A", "1.2", 3), Released("A", "1.1", 5), installedA),
            Mod(Released("B", "2.1", 1), Version("B", "2.0")),
            Mod(Released("X", "3.0", 2), Version("X", "2.0")),
            Mod(Released("N", "0.1", 4))
        }.ToDictionary(m => m.Id);
        var changes = new CkanChanges
        {
            FromCommit = "a3f9c21aaaa", ToCommit = "7d01e4bbbbb", FromSync = Now.AddHours(-14).AddMinutes(-20),
            ToSync = Now.AddMinutes(-12),
            NewVersions = [new("A", "A-1.1"), new("A", "A-1.2"), new("B", "B-2.1"), new("X", "X-3.0")],
            NewMods = [new("N", "N-0.1"), new("Gone", "Gone-1.0")]
        };
        var data = new HomeData
        {
            Instances =
            [
                new InstanceCheckResult { Folder = "/tmp/rp1", Updates = [new("A", "1.0", "1.2")] },
                new InstanceCheckResult { Folder = "/tmp/visual", Updates = [new("A", "1.0", "1.1")] }
            ]
        };

        var feed = HomeCkanFeed.Build(changes, mods, [rp1, visual], data, Now);

        Assert.Equal(("a3f9c21 → 7d01e4b", " · yesterday 21:40 → 12 min ago"), (feed.CommitsText, feed.TimesText));
        var a = Assert.Single(feed.YourMods);
        Assert.Equal(("A", "3 h ago", "1.0", "1.2"), (a.Name, a.When, a.From, a.To));
        Assert.Equal(["RP-1", "Visual"], a.Instances);
        Assert.Equal(["1.2", "1.1"], a.Updates.Select(u => u.Target.Version));

        Assert.Equal(["B", "X", "A"], feed.AllUpdates.Select(r => r.Name));
        Assert.Equal(("2.0", "2.1", false), (feed.AllUpdates[0].From, feed.AllUpdates[0].To, feed.AllUpdates[0].CanUpdate));
        Assert.Equal(["RP-1"], feed.AllUpdates[0].Instances);
        Assert.Empty(feed.AllUpdates[1].Instances);
        Assert.Equal("1.0", feed.AllUpdates[2].From);

        var n = Assert.Single(feed.NewMods);
        Assert.Equal(("N", "0.1", true), (n.Name, n.To, n.IsNewMod));
    }

    [Fact]
    public void Without_a_saved_diff_the_feed_is_empty() =>
        Assert.False(HomeCkanFeed.Build(null, new Dictionary<string, Mod>(), [], new HomeData(), Now).HasChanges);

    [Fact]
    public void Short_names_use_the_first_word_unless_it_is_shared()
    {
        var a = new Instance("RP-1 Express", "/tmp/a", "1.12.5");
        var b = new Instance("Career one", "/tmp/b", "1.12.5");
        var c = new Instance("Career two", "/tmp/c", "1.12.5");

        var names = HomeCkanFeed.ShortNames([a, b, c]);

        Assert.Equal(["RP-1", "Career one", "Career two"], [names[a], names[b], names[c]]);
    }

    [Fact]
    public void An_update_keeps_the_picked_flag_and_reason()
    {
        var installed = Version("A", "1.0");
        var instance = Instance("1.12.5", installed);
        instance.ModRecords["A"] = new InstalledModRecord { IsUserRequested = false, Reason = "for B" };

        var planned = InstalledMods.PlanUpdate(instance, installed, Version("A", "1.1"));

        Assert.Equal(("1.1", false, true, "for B", null),
            (planned.Version.Version, planned.IsUserRequested, planned.IsVersionPickedByUser, planned.OptionalReason,
                planned.Replaces));
    }
}
