using KMRLauncherMvvm.Models;
using KMRLauncherMvvm.Services;
using KMRLauncherMvvm.ViewModels.InstallMods;
using static KMRLauncherMvvm.Tests.TestData;

namespace KMRLauncherMvvm.Tests;

public class InstanceServiceTests : IDisposable
{
    private readonly string _temp = Path.Combine(Path.GetTempPath(), "kmr-service-" + Guid.NewGuid());
    private readonly string _file;

    public InstanceServiceTests()
    {
        _file = Path.Combine(_temp, "config", "settings.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_temp)) Directory.Delete(_temp, true);
    }

    private Instance Add(InstanceService service, string name)
    {
        var root = Path.Combine(_temp, name);
        Directory.CreateDirectory(root);
        var instance = new Instance(name, root, "1.12.5") { PrefixPath = root + "-prefix" };
        service.Instances.Add(instance);
        service.Save(instance, modsChanged: true);
        return instance;
    }

    [Fact]
    public void CreatesTheSettingsFileWhenItIsMissing()
    {
        var service = new InstanceService(_file);

        Assert.True(File.Exists(_file));
        Assert.Empty(service.Instances);
        Assert.Null(service.Primary);
    }

    [Fact]
    public void SaveWritesTheInstanceAndRaisesChanged()
    {
        var service = new InstanceService(_file);
        var changes = 0;
        service.Changed += () => changes++;

        var rp1 = Add(service, "rp1");

        Assert.Equal(1, changes);
        Assert.True(File.Exists(Path.Combine(rp1.RootPath, ".kmr", "instance.json")));
        Assert.Equal("rp1", Assert.Single(new InstanceService(_file).Instances).Name);
    }

    [Fact]
    public void PrimaryIsTheMarkedUsableInstanceOrTheFirstUsableOne()
    {
        var service = new InstanceService(_file);
        var a = Add(service, "a");
        var b = Add(service, "b");

        Assert.Same(a, service.Primary);

        service.MakePrimary(b);
        Assert.True(b.IsPrimary);
        Assert.False(a.IsPrimary);
        Assert.Equal("b", new InstanceService(_file).Primary?.Name);

        Directory.Delete(b.RootPath, true);
        Assert.Equal("a", new InstanceService(_file).Primary?.Name);
    }

    [Fact]
    public void ForgetKeepsTheFolderAndRemovesTheInstanceFromTheList()
    {
        var service = new InstanceService(_file);
        var a = Add(service, "a");
        Add(service, "b");

        service.Forget(a);

        Assert.True(Directory.Exists(a.RootPath));
        Assert.Equal(["b"], new InstanceService(_file).Instances.Select(i => i.Name));
    }

    [Fact]
    public void ForgetPutsTheInstanceBackWhenSavingFails()
    {
        var service = new InstanceService(_file);
        var a = Add(service, "a");
        var b = Add(service, "b");
        File.Delete(_file);
        Directory.CreateDirectory(_file);

        Assert.ThrowsAny<IOException>(() => service.Forget(a));

        Assert.Equal([a, b], service.Instances);
    }

    [Fact]
    public void RecordPlayAddsThePlayTimeAndSavesIt()
    {
        var service = new InstanceService(_file);
        var a = Add(service, "a");
        a.PlayTime = 100;
        var started = new DateTime(2026, 11, 21, 20, 0, 0);

        service.RecordPlay(a, started, started.AddSeconds(90));

        Assert.Equal(190, a.PlayTime);
        Assert.Equal(started.AddSeconds(90), a.LastPlayed);
        Assert.Equal(190, InstanceStore.Load(a.RootPath).PlayTime);
    }

    [Fact]
    public void OnlyAManagedPrefixThatNoOtherInstanceUsesIsOwned()
    {
        var service = new InstanceService(_file);
        var a = Add(service, "a");
        var b = Add(service, "b");
        a.PrefixPath = Path.Combine(WinePrefixes.Root, "a");
        b.PrefixPath = Path.Combine(WinePrefixes.Root, "b");

        Assert.True(service.OwnsPrefix(a));

        b.PrefixPath = a.PrefixPath;
        Assert.False(service.OwnsPrefix(a));

        a.PrefixPath = Path.Combine(_temp, "own-prefix");
        Assert.False(service.OwnsPrefix(a));
    }

    [Fact]
    public async Task DeleteRemovesTheFolderAndTheInstanceButNotAnUnmanagedPrefix()
    {
        var service = new InstanceService(_file);
        var a = Add(service, "a");
        Directory.CreateDirectory(a.PrefixPath);

        await service.DeleteAsync(a);

        Assert.False(Directory.Exists(a.RootPath));
        Assert.True(Directory.Exists(a.PrefixPath));
        Assert.Empty(new InstanceService(_file).Instances);
    }

    [Fact]
    public void ClosingADialogDisposesIt()
    {
        var dialogs = new DialogService();
        var dialog = new DisposableDialog();
        dialogs.Show(dialog);

        dialogs.Close(dialog);

        Assert.True(dialog.Disposed);
    }

    [Fact]
    public async Task NewInstanceInTheInstanceStepSelectsTheCreatedInstance()
    {
        var a = new Instance("A", Path.Combine(_temp, "a"), "1.12.5");
        var b = new Instance("B", Path.Combine(_temp, "b"), "1.12.5");
        var usable = new List<Instance> { a };
        var data = new InstallModsData { RequestedModVersions = [Requested(Version("Foo", "1.0"))] };
        var step = new InstallModsSelectInstanceStepViewModel(data, new TestServices().Dependency, () => usable)
        {
            CreateInstance = () =>
            {
                usable.Add(b);
                return Task.FromResult<Instance?>(b);
            }
        };
        Assert.Same(a, data.SelectedInstance);

        await step.NewInstanceCommand.ExecuteAsync(null);

        Assert.Equal([a, b], step.Instances);
        Assert.Same(b, data.SelectedInstance);
        Assert.Equal(2, step.Cards.Count);
    }

    private sealed class DisposableDialog : IDisposable
    {
        public bool Disposed { get; private set; }
        public void Dispose() => Disposed = true;
    }
}
