using KMRLauncherMvvm.Models;
using KMRLauncherMvvm.ViewModels.InstallMods;
using static KMRLauncherMvvm.Tests.TestData;

namespace KMRLauncherMvvm.Tests;

// The Recommends step without UI: OnEntering fills the lists, OnLeaving adds the ticked rows
public class RecommendsStepTests
{
    private static (InstallModsRecommendsStepViewModel step, InstallModsData data) Enter(TestServices services,
        Instance instance, ModVersion host)
    {
        var data = new InstallModsData { SelectedInstance = instance, RequestedModVersions = [Requested(host)] };
        var step = new InstallModsRecommendsStepViewModel(data, services.ModList, services.Dependency);
        step.OnEntering(null);
        return (step, data);
    }

    private static InstallModsModulesStepViewModel Modules(TestServices services, InstallModsData data) =>
        new(data, services.Compatibility, services.Dependency);

    [Fact]
    public void Recommendations_start_selected_and_suggestions_unselected()
    {
        var host = Version("Host", "1.0");
        host.Recommends = [Rel("Rec")];
        host.Suggests = [Rel("Sug")];
        var services = new TestServices(Mod(host), Mod(Version("Rec", "1.0")), Mod(Version("Sug", "1.0")));

        var (step, _) = Enter(services, Instance(), host);

        Assert.True(Assert.Single(step.Recommendations).IsSelected);
        Assert.False(Assert.Single(step.Suggestions).IsSelected);
    }

    [Fact]
    public void Selected_rows_and_their_dependencies_are_added_when_leaving()
    {
        var host = Version("Host", "1.0");
        host.Recommends = [Rel("Rec")];
        host.Suggests = [Rel("Sug")];
        var services = new TestServices(Mod(host), Mod(Version("Rec", "1.0", depends: [Rel("RecLib")])),
            Mod(Version("RecLib", "1.0")), Mod(Version("Sug", "1.0")));

        var (step, data) = Enter(services, Instance(), host);
        step.OnLeaving(Modules(services, data));

        var rec = Assert.Single(data.FinalModList, m => m.Version.Identifier == "Rec");
        Assert.Equal("Recommended by Host", rec.ReasonForAction);
        Assert.False(rec.IsUserRequested);
        Assert.Contains(data.FinalModList, m => m.Version.Identifier == "RecLib");
        Assert.DoesNotContain(data.FinalModList, m => m.Version.Identifier == "Sug");
    }

    [Fact]
    public void Unselected_recommendation_is_not_added()
    {
        var host = Version("Host", "1.0");
        host.Recommends = [Rel("Rec")];
        var services = new TestServices(Mod(host), Mod(Version("Rec", "1.0")));

        var (step, data) = Enter(services, Instance(), host);
        step.Recommendations[0].IsSelected = false;
        step.OnLeaving(Modules(services, data));

        Assert.DoesNotContain(data.FinalModList, m => m.Version.Identifier == "Rec");
    }

    [Fact]
    public void Any_of_of_a_selected_recommendation_shows_up_on_the_Modules_step()
    {
        var host = Version("Host", "1.0");
        host.Recommends = [Rel("Rec")];
        var services = new TestServices(Mod(host), Mod(Version("Rec", "1.0", depends: [AnyOf(Entry("X"), Entry("Y"))])),
            Mod(Version("X", "1.0")), Mod(Version("Y", "1.0")));

        var (step, data) = Enter(services, Instance(), host);
        var modules = Modules(services, data);
        step.OnLeaving(modules);
        modules.OnEntering(step);

        Assert.Single(modules.Choices);
        Assert.Contains(data.FinalModList, m => m.Version.Identifier == "Rec");
    }

    [Fact]
    public void Same_mod_recommended_twice_is_one_row_with_both_sources()
    {
        var a = Version("A", "1.0");
        a.Recommends = [Rel("Rec")];
        var b = Version("B", "1.0");
        b.Recommends = [Rel("Rec")];
        var services = new TestServices(Mod(a), Mod(b), Mod(Version("Rec", "1.0")));

        var data = new InstallModsData { SelectedInstance = Instance(), RequestedModVersions = [Requested(a), Requested(b)] };
        var step = new InstallModsRecommendsStepViewModel(data, services.ModList, services.Dependency);
        step.OnEntering(null);
        step.OnLeaving(Modules(services, data));

        Assert.Equal(new[] { "A", "B" }, Assert.Single(step.Recommendations).Sources.Order());
        Assert.Single(data.FinalModList, m => m.Version.Identifier == "Rec");
    }

    [Fact]
    public void Back_and_next_keeps_the_selection_and_does_not_add_twice()
    {
        var host = Version("Host", "1.0");
        host.Recommends = [Rel("Rec")];
        host.Suggests = [Rel("Sug")];
        var services = new TestServices(Mod(host), Mod(Version("Rec", "1.0")), Mod(Version("Sug", "1.0")));

        var (step, data) = Enter(services, Instance(), host);
        step.Recommendations[0].IsSelected = false;
        step.Suggestions[0].IsSelected = true;
        var modules = Modules(services, data);
        step.OnLeaving(modules);

        // Back from the Modules step, then Next again
        step.OnEntering(modules);
        Assert.False(step.Recommendations[0].IsSelected);
        Assert.True(step.Suggestions[0].IsSelected);

        step.OnLeaving(modules);
        Assert.Single(data.FinalModList, m => m.Version.Identifier == "Sug");
        Assert.DoesNotContain(data.FinalModList, m => m.Version.Identifier == "Rec");
    }
}