using KMRLauncherMvvm.Models;
using KMRLauncherMvvm.ViewModels.InstallMods;
using static KMRLauncherMvvm.Tests.TestData;

namespace KMRLauncherMvvm.Tests;

// Phase 5 step 5: the install flow as a dialog (texts and numbers worked out without the UI)
public class InstallDialogTests
{
    [Fact]
    public void Fit_hint_names_a_mod_that_doesnt_fit()
    {
        var old = new Instance("Old", "/tmp/old", "1.12.3");
        var picked = new[] { Requested(Version("Kopernicus", "1.0", ksp: "1.12.5", strict: true)), Requested(Version("A", "1.0")) };

        var (text, kind) = InstallChanges.FitHint(old, picked);

        Assert.Equal("Kopernicus not for 1.12.3", text);
        Assert.Equal(FitKind.Warning, kind);
    }

    [Fact]
    public void Fit_hint_counts_already_installed_versions()
    {
        var a = Version("A", "1.0");
        var instance = Instance("1.12.5", a);

        Assert.Equal(("1 already installed", FitKind.Installed),
            InstallChanges.FitHint(instance, [Requested(a), Requested(Version("B", "1.0"))]));
        Assert.Equal(("All 2 fit", FitKind.Fits),
            InstallChanges.FitHint(Instance(), [Requested(a), Requested(Version("B", "1.0"))]));
    }

    [Fact]
    public void Reasons_follow_the_mockup()
    {
        var parallax = new PlannedModVersion { Version = Version("Parallax", "2.0.8"), IsUserRequested = true };
        var harmony = new PlannedModVersion { Version = Version("HarmonyKSP", "2.2.1"), RequiredBy = ["Parallax-2.0.8"] };
        var extra = new PlannedModVersion
        {
            Version = Version("RestockPlus", "1.4.3"), IsUserRequested = true, OptionalReason = "Recommended by Restock"
        };
        List<PlannedModVersion> all = [parallax, harmony, extra];

        Assert.Equal("Picked by you", InstallChanges.Reason(parallax, null, all));
        Assert.Equal("Dependency of Parallax", InstallChanges.Reason(harmony, null, all));
        Assert.Equal("Recommended by Restock", InstallChanges.Reason(extra, null, all));
    }

    [Fact]
    public void Update_rows_shorten_long_versions()
    {
        var row = new ChangeRow(ChangeKind.Update, "Kopernicus", "Picked by you", "release-1.12.1-201",
            "release-1.12.1-200", 3 * 1024 * 1024, false);

        Assert.Equal("release-1.12.1-200 → -201", row.ChangeText);
        Assert.Equal("replaces -200", row.ReplacesText);
        Assert.Equal("3.0 MB", row.SizeText);
    }

    [Fact]
    public void Rows_put_updates_first_then_new_mods_by_name()
    {
        var data = new InstallModsData
        {
            SelectedInstance = Instance("1.12.5", Version("Waterfall", "0.10.3")),
            FinalModList =
            [
                Requested(Version("Restock", "1.4.3")),
                Requested(Version("Waterfall", "0.10.4")),
                Requested(Version("Parallax", "2.0.8"))
            ]
        };

        var rows = InstallChanges.Rows(data, _ => false);

        Assert.Equal(["Waterfall", "Parallax", "Restock"], rows.Select(r => r.Name));
        Assert.Equal(new ChangeCounts(2, 1, 0, 0, 0), InstallChanges.Count(rows));
    }

    [Fact]
    public void Names_and_durations()
    {
        Assert.Equal("A and 2 more", InstallChanges.JoinNames(["A", "B", "C"]));
        Assert.Equal("A, B and C", InstallChanges.JoinAll(["A", "B", "C"]));
        Assert.Equal("1 min 42 s", InstallChanges.Duration(TimeSpan.FromSeconds(102)));
        Assert.Equal("42 s", InstallChanges.Duration(TimeSpan.FromSeconds(42)));
    }

    [Fact]
    public void Failed_row_detail_sums_up_mirror_errors()
    {
        const string twoMirrors = "Scatterer v0.0838 could not be downloaded.\n  github.com      no data for 30 s\n" +
                                  "  spacedock.info  HTTP 503 Service Unavailable";
        const string oneMirror = "A 1.0 could not be downloaded.\n  github.com  server answered 404 NotFound";

        Assert.Equal("All mirrors failed", InstallResultViewModel.ShortError(twoMirrors));
        Assert.Equal("server answered 404 NotFound", InstallResultViewModel.ShortError(oneMirror));
        Assert.Equal("Installing A failed: disk full", InstallResultViewModel.ShortError("Installing A failed: disk full"));
    }

    [Fact]
    public void Picking_an_option_radio_selects_it_in_the_group()
    {
        var relationship = AnyOf(Entry("A"), Entry("B"));
        ChoiceOptionViewModel Option(string id) =>
            new(new PlannedModVersion { Version = Version(id, "1.0"), Relationship = relationship }, []);
        var group = new ChoiceGroupViewModel(relationship, [Option("A"), Option("B")], ["Host 1.0"], ["Host"]);

        group.Options[1].IsSelected = true;

        Assert.Same(group.Options[1], group.SelectedOption);
        Assert.Equal("B", group.SelectedPlan?.Version.Identifier);
        Assert.False(group.Options[0].IsSelected);
        Assert.Equal("Needed by Host 1.0", group.RequiredByText);
        Assert.Equal("Host also needs one of A, B. You pick it in step 3.",
            InstallDialogViewModel.ChoicesHint([group], 3));
    }

    [Fact]
    public void Virtual_choice_is_titled_by_its_name()
    {
        var relationship = Rel("Parallax-Textures");
        var group = new ChoiceGroupViewModel(relationship,
            [new ChoiceOptionViewModel(new PlannedModVersion { Version = Version("Stock", "1.0") }, [])], []);

        Assert.True(group.IsVirtual);
        Assert.Equal("Parallax-Textures", group.Title);
    }

    [Fact]
    public void Single_option_choices_are_listed_as_picked_for_you()
    {
        var host = Version("Host", "1.0", depends: [Rel("Harmony2")]);
        var provider = Version("HarmonyKSP", "2.2.1", provides: ["Harmony2"]);
        var services = new TestServices(Mod(host), Mod(provider));
        var data = new InstallModsData { SelectedInstance = Instance(), RequestedModVersions = [Requested(host)] };
        services.Dependency.ResolveDependencies(data);

        var step = new InstallModsModulesStepViewModel(data, services.Compatibility, services.Dependency);
        step.Prepare();

        var row = Assert.Single(step.PickedForYou);
        Assert.Equal(new PickedForYouRow("Harmony2", "HarmonyKSP 2.2.1", "only mod that provides it"), row);
        Assert.True(step.ShouldSkip);
    }
}
