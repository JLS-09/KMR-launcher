using KMRLauncherMvvm.Models;
using KMRLauncherMvvm.Services;
using static KMRLauncherMvvm.Tests.TestData;

namespace KMRLauncherMvvm.Tests;

// Spec v1.34 suppress_recommendations
public class SuppressRecommendationsTests
{
    private static readonly ModVersion A = Version("A", "1.0", depends: [Rel("B", suppress: true)]);
    private static readonly ModVersion B = Version("B", "1.0", depends: [Rel("C")]);
    private static readonly ModVersion C = Version("C", "1.0");
    private static readonly ModVersion D = Version("D", "1.0", depends: [Rel("C")]);

    private static HashSet<string> Suppressed(params ModVersion[] requested)
    {
        var data = new InstallModsData
        {
            SelectedInstance = Instance(),
            RequestedModVersions = [.. requested.Select(Requested)]
        };
        new TestServices(Mod(A), Mod(B), Mod(C), Mod(D)).Dependency.ResolveDependencies(data);
        return [.. data.ModsWithIgnoredRecommendations];
    }

    [Fact]
    public void Satisfying_mod_and_its_sole_dependencies_are_suppressed()
    {
        Assert.Equal(new[] { "B", "C" }, Suppressed(A).Order());
    }

    [Fact]
    public void Dependency_that_something_else_needs_is_not_suppressed()
    {
        Assert.Equal(new[] { "B" }, Suppressed(A, D));
    }

    [Fact]
    public void Mod_the_user_requested_keeps_its_recommendations()
    {
        // Decided (c): a direct request wins
        Assert.Empty(Suppressed(A, B));
    }

    [Fact]
    public void Applies_to_the_chosen_any_of_option_and_to_providers()
    {
        var e = Version("E", "1.0", depends:
        [
            new Relationship { AnyOf = [Entry("X"), Entry("Y")], SuppressRecommendations = true },
            Rel("Virtual", suppress: true)
        ]);
        List<PlannedModVersion> finalList =
        [
            Requested(e),
            new() { Version = Version("X", "1.0"), RequiredBy = ["E-1.0"] },
            new() { Version = Version("P", "1.0", provides: ["Virtual"]), RequiredBy = ["E-1.0"] }
        ];

        Assert.Equal(new[] { "P", "X" }, DependencyService.FindSuppressedRecommendations(finalList).Order());
    }
}
