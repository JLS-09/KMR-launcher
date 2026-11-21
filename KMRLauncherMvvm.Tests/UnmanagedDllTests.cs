using KMRLauncherMvvm.Models;
using KMRLauncherMvvm.Services;
using static KMRLauncherMvvm.Tests.TestData;

namespace KMRLauncherMvvm.Tests;

public class UnmanagedDllTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "kmr-dlls-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    private static Instance WithDlls(Instance instance, params string[] identifiers)
    {
        instance.UnmanagedDlls = identifiers.ToDictionary(i => i, i => $"GameData/{i}.dll");
        return instance;
    }

    private static InstallModsData Resolve(TestServices services, Instance instance, params PlannedModVersion[] requested)
    {
        var data = new InstallModsData { SelectedInstance = instance, RequestedModVersions = [.. requested] };
        services.Dependency.ResolveDependencies(data);
        return data;
    }

    [Theory]
    [InlineData("GameData/ModuleManager.4.2.3.dll", "ModuleManager")]
    [InlineData("GameData/Kopernicus/Plugins/Kopernicus.Parser.dll", "Kopernicus")]
    [InlineData("GameData/000_Harmony/0Harmony.DLL", "0Harmony")]
    [InlineData("GameData/Foo/_my_mod.dll", "my-mod")]
    [InlineData("Plugins/Outside.dll", null)]
    [InlineData("GameData/Foo/readme.txt", null)]
    public void IdentifierFollowsCkansRule(string path, string? expected) =>
        Assert.Equal(expected, UnmanagedDlls.IdentifierOf(path));

    [Fact]
    public void StockOwnedAndInstalledDllsAreSkipped()
    {
        var instance = Instance("1.12.5", Version("Kopernicus", "1.0"));
        instance.ModRecords["Parallax"] = new InstalledModRecord { Files = ["GameData/Parallax/Parallax.dll"] };

        var found = UnmanagedDlls.Detect(
        [
            "GameData/Squad/Plugins/KSPSteamCtrlr.dll",
            "GameData/SquadExpansion/Serenity/Serenity.dll",
            "GameData/Parallax/Parallax.dll",
            "GameData/Kopernicus/Plugins/Kopernicus.dll",
            "GameData/ModuleManager.4.2.3.dll",
            "GameData/ModuleManager.4.2.2.dll"
        ], instance);

        Assert.Equal(new Dictionary<string, string> { ["ModuleManager"] = "GameData/ModuleManager.4.2.2.dll" }, found);
    }

    [Fact]
    public void ScanReadsGameData()
    {
        var instance = new Instance("Disk", _root, "1.12.5");
        Directory.CreateDirectory(Path.Combine(_root, "GameData", "Hand"));
        File.WriteAllText(Path.Combine(_root, "GameData", "Hand", "HandMade.dll"), "x");

        UnmanagedDlls.Refresh(instance);

        Assert.Equal(["HandMade"], instance.UnmanagedDlls.Keys);
    }

    [Fact]
    public void DependencyInstalledByHandIsNotInstalledAgainWhateverItsVersion()
    {
        var main = Version("Main", "1.0", depends: [Rel("ModuleManager", min: "4.2.0")]);
        var services = new TestServices(Mod(main), Mod(Version("ModuleManager", "4.2.3")));

        var data = Resolve(services, WithDlls(Instance(), "ModuleManager"), Requested(main));

        Assert.Empty(data.Problems);
        Assert.Equal(["Main"], data.FinalModList.Select(m => m.Version.Identifier));
    }

    [Fact]
    public void AnyOfWithAnOptionInstalledByHandNeedsNoChoice()
    {
        var host = Version("Host", "1.0", depends: [AnyOf(Entry("X"), Entry("Y"))]);
        var services = new TestServices(Mod(host), Mod(Version("X", "1.0")), Mod(Version("Y", "1.0")));

        var data = Resolve(services, WithDlls(Instance(), "Y"), Requested(host));

        Assert.Empty(data.ChoosableVersions);
        Assert.Empty(data.Problems);
    }

    [Fact]
    public void ModYouPickYourselfIsInstalledOverItsDll()
    {
        var mm = Version("ModuleManager", "4.2.3");

        var data = Resolve(new TestServices(Mod(mm)), WithDlls(Instance(), "ModuleManager"), Requested(mm));

        Assert.Equal(["ModuleManager"], data.FinalModList.Select(m => m.Version.Identifier));
    }

    [Fact]
    public void ConflictWithAModInstalledByHandIsAProblem()
    {
        var main = Version("Main", "1.0", conflicts: [Rel("OldThing")]);

        var data = Resolve(new TestServices(Mod(main)), WithDlls(Instance(), "OldThing"), Requested(main));

        var problem = Assert.Single(data.Problems);
        Assert.True(problem.IsConflict);
        Assert.Contains("installed by hand (GameData/OldThing.dll)", problem.Message);
    }

    [Fact]
    public void DllKeepsDependsMetWhenTheManagedModGoes()
    {
        var user = Version("User", "1.0", depends: [Rel("Lib")]);
        var lib = Version("Lib", "1.0");
        var dlls = new Dictionary<string, string> { ["Lib"] = "GameData/Lib.dll" };

        Assert.NotEmpty(CompatibilityService.FindBrokenDepends([], [user], [user, lib]));
        Assert.Empty(CompatibilityService.FindBrokenDepends([], [user], [user, lib], dlls));
    }

    [Fact]
    public void RemovalKeepsModsWhoseDependencyIsInstalledByHand()
    {
        var host = Version("Host", "1.0", depends: [AnyOf(Entry("X"), Entry("Y"))]);
        var x = Version("X", "1.0");
        var instance = WithDlls(Instance("1.12.5", host, x), "Y");
        instance.ModRecords["Host"] = new InstalledModRecord { IsUserRequested = true };
        instance.ModRecords["X"] = new InstalledModRecord { IsUserRequested = true };

        var plan = InstalledMods.PlanRemoval(instance, ["X"]);

        Assert.Equal(["X"], plan.All.Select(m => m.Identifier));
    }
}
