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
        var step = new InstallModsRecommendsStepViewModel(data, services.ModList, services.Compatibility,
            services.Dependency);
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
        Assert.True(rec.IsUserRequested);
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
        var step = new InstallModsRecommendsStepViewModel(data, services.ModList, services.Compatibility,
            services.Dependency);
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
    
    [Fact]
    public void Ticked_recommendation_keeps_its_version_when_an_any_of_wants_another()
    {
        // Rec 2.0 is ticked; the any_of accepts Rec 1.0 or older, or Alt. Rec isn't swapped, so Alt is picked.
        var host = Version("Host", "1.0", depends: [AnyOf(Entry("Rec", max: "1.0"), Entry("Alt"))]);
        host.Recommends = [Rel("Rec")];
        var services = new TestServices(Mod(host), Mod(Version("Rec", "2.0"), Version("Rec", "1.0")),
            Mod(Version("Alt", "1.0")));

        var (step, data) = Enter(services, Instance(), host);
        var modules = Modules(services, data);
        step.OnLeaving(modules);
        modules.OnEntering(step);

        Assert.Empty(modules.Choices);
        Assert.Contains(data.FinalModList, m => m.Version.Id == "Rec-2.0");
        Assert.DoesNotContain(data.FinalModList, m => m.Version.Id == "Rec-1.0");
        Assert.Contains(data.FinalModList, m => m.Version.Identifier == "Alt");
    }

    [Fact]
    public void Recommendation_offers_only_versions_within_its_limits()
    {
        var host = Version("Host", "1.0");
        host.Recommends = [Rel("Rec", max: "2.0")];
        var services = new TestServices(Mod(host),
            Mod(Version("Rec", "3.0"), Version("Rec", "2.0"), Version("Rec", "1.0"), Version("Rec", "0.5", "1.9")));

        var (step, _) = Enter(services, Instance(), host);

        var row = Assert.Single(step.Recommendations);
        Assert.Equal(new[] { "Rec-2.0", "Rec-1.0" }, row.Versions.Select(v => v.Id));
        Assert.Equal("Rec-2.0", row.SelectedVersion!.Id);
    }

    [Fact]
    public void Picked_version_is_added_and_ticks_the_row()
    {
        var host = Version("Host", "1.0");
        host.Suggests = [Rel("Sug")];
        var services = new TestServices(Mod(host), Mod(Version("Sug", "2.0"), Version("Sug", "1.0")));

        var (step, data) = Enter(services, Instance(), host);
        var row = Assert.Single(step.Suggestions);
        Assert.False(row.IsSelected);

        row.SelectedVersion = row.Versions.Single(v => v.Id == "Sug-1.0");
        Assert.True(row.IsSelected);

        step.OnLeaving(Modules(services, data));
        Assert.Contains(data.FinalModList, m => m.Version.Id == "Sug-1.0");
    }

    [Fact]
    public void Picked_version_is_kept_on_back_and_next()
    {
        var host = Version("Host", "1.0");
        host.Recommends = [Rel("Rec")];
        var services = new TestServices(Mod(host), Mod(Version("Rec", "2.0"), Version("Rec", "1.0")));

        var (step, data) = Enter(services, Instance(), host);
        step.Recommendations[0].SelectedVersion = step.Recommendations[0].Versions[1];
        var modules = Modules(services, data);
        step.OnLeaving(modules);
        step.OnEntering(modules);

        Assert.Equal("Rec-1.0", step.Recommendations[0].SelectedVersion!.Id);
    }

    [Fact]
    public void Two_recommenders_only_get_versions_that_fit_both()
    {
        var a = Version("A", "1.0");
        a.Recommends = [Rel("Rec", min: "2.0")];
        var b = Version("B", "1.0");
        b.Recommends = [Rel("Rec", max: "2.0")];
        var services = new TestServices(Mod(a), Mod(b),
            Mod(Version("Rec", "3.0"), Version("Rec", "2.0"), Version("Rec", "1.0")));

        var data = new InstallModsData { SelectedInstance = Instance(), RequestedModVersions = [Requested(a), Requested(b)] };
        var step = new InstallModsRecommendsStepViewModel(data, services.ModList, services.Compatibility,
            services.Dependency);
        step.OnEntering(null);

        Assert.Equal("Rec-2.0", Assert.Single(Assert.Single(step.Recommendations).Versions).Id);
    }

    [Fact]
    public void Supporting_mod_is_one_row_with_only_its_supporting_compatible_versions()
    {
        var host = Version("Host", "1.0");
        var sup3 = Version("Sup", "3.0");
        var sup2 = Version("Sup", "2.0");
        sup2.Supports = [Rel("Host")];
        var sup1 = Version("Sup", "1.0");
        sup1.Supports = [Rel("Host")];
        var supOld = Version("Sup", "0.5", "1.9");
        supOld.Supports = [Rel("Host")];
        var queued = Version("Queued", "1.0");
        queued.Supports = [Rel("Host")];
        host.Depends = [Rel("Queued")];
        var services = new TestServices(Mod(host), Mod(sup3, sup2, sup1, supOld), Mod(queued));

        var (step, _) = Enter(services, Instance(), host);

        var row = Assert.Single(step.Supported);
        Assert.Equal("Sup", row.Identifier);
        Assert.Equal(new[] { "Sup-2.0", "Sup-1.0" }, row.Versions.Select(v => v.Id));
        Assert.False(row.IsSelected);
    }
}