using KMRLauncherMvvm.Models;
using KMRLauncherMvvm.ViewModels.InstallMods;
using static KMRLauncherMvvm.Tests.TestData;

namespace KMRLauncherMvvm.Tests;

// The Modules step without UI: build it from InstallModsData and call Prepare like the wizard does
public class ModulesStepTests
{
    private static (InstallModsModulesStepViewModel step, InstallModsData data) Enter(TestServices services,
        Instance instance, ModVersion host)
    {
        var data = new InstallModsData { SelectedInstance = instance, RequestedModVersions = [Requested(host)] };
        services.Dependency.ResolveDependencies(data);

        var step = new InstallModsModulesStepViewModel(data, services.Compatibility, services.Dependency);
        step.Prepare();
        return (step, data);
    }

    [Fact]
    public void Choice_filtered_to_one_option_by_ksp_is_picked_automatically()
    {
        // Phase 1 task 8: three options, only one supports KSP 1.12
        var host = Version("Host", "1.0", depends: [AnyOf(Entry("X"), Entry("Y"), Entry("Z"))]);
        var services = new TestServices(Mod(host), Mod(Version("X", "1.0", "1.9")), Mod(Version("Y", "1.0")),
            Mod(Version("Z", "1.0", "1.10")));

        var (step, data) = Enter(services, Instance(), host);

        Assert.Empty(step.Choices);
        Assert.Contains(data.FinalModList, m => m.Version.Identifier == "Y");
    }

    [Fact]
    public void Choice_filtered_to_no_options_is_unsatisfiable()
    {
        var host = Version("Host", "1.0", depends: [AnyOf(Entry("X"), Entry("Z"))]);
        var services = new TestServices(Mod(host), Mod(Version("X", "1.0", "1.9")), Mod(Version("Z", "1.0", "1.10")));

        var (step, _) = Enter(services, Instance(), host);

        Assert.True(Assert.Single(step.Choices).IsUnsatisfiable);
        Assert.False(step.CanGoNext);
    }

    [Fact]
    public void Swapped_queued_mod_gets_its_dependencies()
    {
        // LibA 2.0 is queued; the any_of needs LibA <= 1.0, which depends on LibCore
        var libA = Mod(Version("LibA", "2.0"), Version("LibA", "1.0", depends: [Rel("LibCore")]));
        var host = Version("Host", "1.0", depends: [Rel("LibA"), AnyOf(Entry("LibA", max: "1.0"), Entry("Alt"))]);
        var services = new TestServices(Mod(host), libA, Mod(Version("LibCore", "1.0")), Mod(Version("Alt", "1.0")));

        var (step, data) = Enter(services, Instance(), host);

        Assert.Empty(step.Choices);
        Assert.Contains(data.FinalModList, m => m.Version.Id == "LibA-1.0");
        Assert.Contains(data.FinalModList, m => m.Version.Identifier == "LibCore");
    }

    [Fact]
    public void Picking_a_conflicting_option_shows_a_conflict()
    {
        var host = Version("Host", "1.0", depends: [AnyOf(Entry("X"), Entry("Y"))]);
        var services = new TestServices(Mod(host), Mod(Version("X", "1.0", conflicts: [Rel("Installed")])),
            Mod(Version("Y", "1.0")));

        var (step, data) = Enter(services, Instance("1.12.5", Version("Installed", "1.0")), host);
        var choice = Assert.Single(step.Choices);

        choice.SelectedOption = choice.Options.Single(o => o.Identifier == "X");
        Assert.Contains(data.Problems, p => p.IsConflict);
        Assert.False(step.CanGoNext);

        choice.SelectedOption = choice.Options.Single(o => o.Identifier == "Y");
        Assert.Empty(data.Problems);
        Assert.True(step.CanGoNext);
    }
    
    [Fact]
    public void Choice_satisfied_by_an_installed_mod_is_dropped()
    {
        // Phase 1 task 7: by identifier (within the limits) or by provides
        var host = Version("Host", "1.0", depends: [AnyOf(Entry("X", min: "2.0"), Entry("Y")), Rel("Virtual")]);
        var services = new TestServices(Mod(host), Mod(Version("X", "2.0"), Version("X", "1.0")), Mod(Version("Y", "1.0")),
            Mod(Version("P", "1.0", provides: ["Virtual"])), Mod(Version("Q", "1.0", provides: ["Virtual"])));

        var (satisfied, _) = Enter(services, Instance("1.12.5", Version("X", "2.0"), Version("P", "1.0", provides: ["Virtual"])), host);
        Assert.Empty(satisfied.Choices);

        // X 1.0 is installed but the any_of needs X >= 2.0, so that card stays
        var (tooOld, _) = Enter(services, Instance("1.12.5", Version("X", "1.0"), Version("P", "1.0", provides: ["Virtual"])), host);
        Assert.Single(tooOld.Choices);
    }

    [Fact]
    public void Step_is_skipped_when_nothing_needs_a_choice()
    {
        var host = Version("Host", "1.0", depends: [AnyOf(Entry("X"), Entry("Z"))]);
        var services = new TestServices(Mod(host), Mod(Version("X", "1.0")), Mod(Version("Z", "1.0", "1.9")));

        var (step, data) = Enter(services, Instance(), host);

        Assert.True(step.ShouldSkip);
        Assert.Contains(data.FinalModList, m => m.Version.Identifier == "X");
    }

    [Fact]
    public void Step_is_shown_when_there_is_a_choice()
    {
        var host = Version("Host", "1.0", depends: [AnyOf(Entry("X"), Entry("Y"))]);
        var services = new TestServices(Mod(host), Mod(Version("X", "1.0")), Mod(Version("Y", "1.0")));

        var (step, _) = Enter(services, Instance(), host);

        Assert.False(step.ShouldSkip);
    }

    [Fact]
    public void Step_is_skipped_when_there_is_a_problem_but_no_choice()
    {
        // No choices, but a conflict with an installed mod: the step is skipped and the summary shows the conflict
        var host = Version("Host", "1.0", conflicts: [Rel("Installed")]);
        var services = new TestServices(Mod(host));

        var (step, data) = Enter(services, Instance("1.12.5", Version("Installed", "1.0")), host);

        Assert.Empty(step.Choices);
        Assert.True(step.ShouldSkip);
        Assert.Contains(data.Problems, p => p.IsConflict);
    }
}