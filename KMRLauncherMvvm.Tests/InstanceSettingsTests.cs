using KMRLauncherMvvm.Models;
using KMRLauncherMvvm.Services;
using KMRLauncherMvvm.ViewModels.Instances;

namespace KMRLauncherMvvm.Tests;

// Phase 5 step 4: the instance Settings tab and the Wine prefix folder (task 6)
public class InstanceSettingsTests
{
    private const string Root = "/cfg/kmrLauncher/prefixes";
    private const string Legacy = "/cfg/prefixes";

    private static Instance Make(string name, string folder, string prefix = "") =>
        new(name, $"/games/{folder}", "1.12.5") { PrefixPath = prefix };

    // Prefixes

    [Fact]
    public void NewPrefixIsNamedAfterTheInstanceFolder()
    {
        var instance = Make("RP-1 Express Install", "rp1-express");

        var prefix = WinePrefixes.DefaultFor(instance, [instance], Root, Legacy, _ => false);

        Assert.Equal($"{Root}/rp1-express", prefix);
    }

    [Fact]
    public void ExistingPrefixInTheOldPlaceIsKept()
    {
        var instance = Make("rp-1 test", "rp1");

        var prefix = WinePrefixes.DefaultFor(instance, [instance], Root, Legacy, p => p == $"{Legacy}/rp-1 test");

        Assert.Equal($"{Legacy}/rp-1 test", prefix);
    }

    [Fact]
    public void PrefixFolderNamesDontClash()
    {
        var first = Make("A", "ksp", $"{Root}/ksp");
        var second = new Instance("B", "/other/ksp", "1.12.5");

        var prefix = WinePrefixes.DefaultFor(second, [first, second], Root, Legacy, _ => false);

        Assert.Equal($"{Root}/ksp-2", prefix);
    }

    [Fact]
    public void AssignMissingOnlyFillsEmptyPrefixes()
    {
        var set = Make("A", "a", "/custom/prefix");
        var missing = Make("B", "b");
        List<Instance> instances = [set, missing];

        Assert.True(WinePrefixes.AssignMissing(instances, Root, Legacy, _ => false));
        Assert.Equal("/custom/prefix", set.PrefixPath);
        Assert.Equal($"{Root}/b", missing.PrefixPath);
        Assert.False(WinePrefixes.AssignMissing(instances, Root, Legacy, _ => false));
    }

    [Fact]
    public void OnlyPrefixesTheLauncherMadeAreManaged()
    {
        Assert.True(WinePrefixes.IsManaged($"{Root}/rp1", Root, Legacy));
        Assert.True(WinePrefixes.IsManaged($"{Legacy}/rp-1 test", Root, Legacy));
        Assert.False(WinePrefixes.IsManaged("/home/jules/Games/prefix", Root, Legacy));
        Assert.False(WinePrefixes.IsManaged(Root, Root, Legacy));
        Assert.False(WinePrefixes.IsManaged("", Root, Legacy));
    }

    [Fact]
    public void SamePathHandlesEmptyPaths()
    {
        Assert.False(WinePrefixes.SamePath("", ""));
        Assert.False(WinePrefixes.SamePath("/a", ""));
        Assert.True(WinePrefixes.SamePath("/a/b/", "/a/b"));
    }

    // A prefix has dosdevices/z: → /. Deleting the prefix must remove the link, not what it points to.
    [Fact]
    public void DeletingAPrefixDoesntFollowSymlinks()
    {
        var temp = Directory.CreateTempSubdirectory("kmr-prefix-test").FullName;
        try
        {
            var target = Directory.CreateDirectory(Path.Combine(temp, "outside")).FullName;
            File.WriteAllText(Path.Combine(target, "keep.txt"), "x");
            var prefix = Directory.CreateDirectory(Path.Combine(temp, "prefix", "dosdevices")).Parent!.FullName;
            Directory.CreateSymbolicLink(Path.Combine(prefix, "dosdevices", "z:"), target);

            Directory.Delete(prefix, true);

            Assert.False(Directory.Exists(prefix));
            Assert.True(File.Exists(Path.Combine(target, "keep.txt")));
        }
        finally
        {
            Directory.Delete(temp, true);
        }
    }

    // Settings tab

    private static (InstanceSettingsViewModel vm, Instance instance, AppSettings settings) Tab()
    {
        var instance = Make("RP-1", "rp1", $"{Root}/rp1");
        instance.CompatibleKspVersions = ["1.12"];
        instance.LaunchArguments = "-force-glcore";
        var other = Make("Vanilla", "vanilla", $"{Root}/vanilla");
        other.IsPrimary = true;
        var settings = new AppSettings { Instances = [instance, other] };
        return (new InstanceSettingsViewModel(instance, settings.Instances), instance, settings);
    }

    [Fact]
    public void NoChangesAtFirst()
    {
        var (vm, _, _) = Tab();

        Assert.False(vm.HasChanges);
        Assert.False(vm.SaveCommand.CanExecute(null));
    }

    [Fact]
    public void GroupsAreNewestFirstWithVersionsOldToNew()
    {
        var (vm, _, _) = Tab();

        Assert.Equal(["1.12", "1.11", "1.10", "1.9", "1.8"], vm.Groups.Select(g => g.Prefix));
        Assert.Equal("1.12.0", vm.Groups[0].Versions[0].Version);
        Assert.True(vm.Groups[0].Versions.Single(v => v.Version == "1.12.5").IsInstanceVersion);
        Assert.True(vm.Groups[0].AllowAll);
        Assert.True(vm.HasOlderGroups);
        Assert.StartsWith("Show 0.", vm.OlderToggleText);

        vm.ToggleOlderCommand.Execute(null);
        Assert.Contains(vm.Groups, g => g.Prefix == "1.7");
    }

    [Fact]
    public void OlderGroupWithATickedVersionIsAlwaysListed()
    {
        var instance = Make("Old", "old", $"{Root}/old");
        instance.CompatibleKspVersions = ["1.7.3"];

        var vm = new InstanceSettingsViewModel(instance, [instance]);

        Assert.Contains(vm.Groups, g => g.Prefix == "1.7");
    }

    [Fact]
    public void EachEditCountsAsAChange()
    {
        var (vm, _, _) = Tab();

        vm.Name = "RP-1 Express";
        vm.IsPrimary = true;
        vm.Groups[1].AllowAll = true;
        vm.Groups[0].Versions[0].IsChecked = true;

        Assert.Equal(4, vm.ChangeCount);
        Assert.Equal("4 unsaved changes", vm.ChangesText);
    }

    [Fact]
    public void TickingAndUntickingAgainIsNoChange()
    {
        var (vm, _, _) = Tab();

        vm.Groups[1].AllowAll = true;
        vm.Groups[1].AllowAll = false;
        vm.LaunchArguments = "-force-glcore ";

        Assert.False(vm.HasChanges);
    }

    [Fact]
    public void EmptyNameCantBeSaved()
    {
        var (vm, _, _) = Tab();

        vm.Name = "  ";

        Assert.True(vm.HasChanges);
        Assert.NotNull(vm.NameError);
        Assert.False(vm.SaveCommand.CanExecute(null));
    }

    [Fact]
    public void ApplyWritesTheEditsAndMakesThisThePrimary()
    {
        var (vm, instance, settings) = Tab();

        vm.Name = " RP-1 Express ";
        vm.IsPrimary = true;
        vm.Prefix = "/custom/prefix";
        vm.LaunchArguments = "";
        vm.Groups[1].AllowAll = true;

        Assert.True(vm.Apply(settings));

        Assert.Equal("RP-1 Express", instance.Name);
        Assert.True(instance.IsPrimary);
        Assert.False(settings.Instances[1].IsPrimary);
        Assert.Equal("/custom/prefix", instance.PrefixPath);
        Assert.Equal("", instance.LaunchArguments);
        Assert.Equal(["1.11", "1.12"], instance.CompatibleKspVersions.Order());
        Assert.False(vm.HasChanges);
    }

    [Fact]
    public void EmptyPrefixMeansTheDefault()
    {
        var (vm, instance, settings) = Tab();
        instance.PrefixPath = "/custom/prefix";
        vm = new InstanceSettingsViewModel(instance, settings.Instances);

        vm.Prefix = "";
        vm.Apply(settings);

        Assert.Equal(WinePrefixes.DefaultFor(instance, settings.Instances), instance.PrefixPath);
    }

    [Fact]
    public void UnknownEntriesAreKept()
    {
        var (_, instance, settings) = Tab();
        instance.CompatibleKspVersions = ["1.12", "1.13"];
        var vm = new InstanceSettingsViewModel(instance, settings.Instances);

        vm.Groups[1].AllowAll = true;
        vm.Apply(settings);

        Assert.Contains("1.13", instance.CompatibleKspVersions);
    }

    [Fact]
    public void RevertRestoresEverything()
    {
        var (vm, _, _) = Tab();

        vm.Name = "Other";
        vm.Prefix = "/x";
        vm.Groups[0].AllowAll = false;
        vm.Groups[0].Versions[2].IsChecked = true;
        vm.RevertCommand.Execute(null);

        Assert.False(vm.HasChanges);
        Assert.Equal("RP-1", vm.Name);
        Assert.True(vm.Groups[0].AllowAll);
        Assert.False(vm.Groups[0].Versions[2].IsChecked);
    }
}
