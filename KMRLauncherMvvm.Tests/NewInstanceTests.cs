using System.IO.Compression;
using KMRLauncherMvvm.Models;
using KMRLauncherMvvm.Services;
using KMRLauncherMvvm.ViewModels.NewInstance;

namespace KMRLauncherMvvm.Tests;

public class NewInstanceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "kmr-new-" + Guid.NewGuid().ToString("N"));
    private readonly List<Instance> _saved = [];

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    private string MakeZip(string name, string folderInZip = "KSP_linux", string buildId = "03190", int files = 3)
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, name);
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        var prefix = folderInZip.Length == 0 ? "" : folderInZip + "/";
        using (var writer = new StreamWriter(archive.CreateEntry(prefix + "buildID64.txt").Open()))
            writer.Write($"build id = {buildId}\nbuild date = 2022-10-04\n");
        for (var i = 0; i < files; i++)
        {
            using var writer = new StreamWriter(archive.CreateEntry($"{prefix}GameData/Squad/file{i}.txt").Open());
            writer.Write(new string('x', 100));
        }

        return path;
    }

    private InstanceCreator Creator(Action<AppSettings, Instance>? save = null) =>
        new(new ZipService(), save ?? ((_, instance) => _saved.Add(instance)));

    private static NewInstanceRequest Request(KspZip zip, string folder, bool primary = false) =>
        new("Career", zip, folder, KspVersions.DefaultCompatibleVersions(zip.Version), primary);

    [Theory]
    [InlineData("Career 1.12.5", "career-1.12.5")]
    [InlineData("  RP-1 / Express  ", "rp-1-express")]
    [InlineData("Ünïcode & stuff!", "ünïcode-stuff")]
    [InlineData("///", "instance")]
    public void SlugMakesSafeFolderNames(string name, string expected) =>
        Assert.Equal(expected, InstanceFolders.Slug(name));

    [Fact]
    public void FreeFolderSkipsTakenFoldersAndInstances()
    {
        var parent = Path.Combine(_root, "instances");
        var existing = new HashSet<string> { Path.Combine(parent, "career") };
        var instances = new[] { new Instance("Other", Path.Combine(parent, "career-2"), "1.12.5") };

        var folder = InstanceFolders.FreeFolder(parent, "Career", instances, existing.Contains);

        Assert.Equal(Path.Combine(parent, "career-3"), folder);
    }

    [Fact]
    public void ParentIsRememberedOrDefault()
    {
        Assert.Equal(InstanceFolders.DefaultParent, InstanceFolders.ParentFor(new AppSettings()));
        Assert.Equal("/games/ksp", InstanceFolders.ParentFor(new AppSettings { InstanceParentFolder = "/games/ksp" }));
    }

    [Theory]
    [InlineData("KSP_linux")]
    [InlineData("")]
    public void ZipIsReadAndMeasured(string folderInZip)
    {
        var zip = new ZipService().GetVersionFromKspZip(MakeZip("ksp.zip", folderInZip));

        Assert.Equal("1.12.5", zip.Version);
        Assert.Equal(folderInZip, zip.RelativeRootPath);
        var (files, bytes) = ZipService.Measure(zip);
        Assert.Equal(4, files);
        Assert.True(bytes > 300);
    }

    [Theory]
    [InlineData("KSP_linux")]
    [InlineData("")]
    public async Task CreatesInstanceFromZip(string folderInZip)
    {
        var settings = new AppSettings();
        var zip = new ZipService().GetVersionFromKspZip(MakeZip("ksp.zip", folderInZip));
        var folder = Path.Combine(_root, "instances", "career");
        var steps = new List<CreateStep>();

        var instance = await Creator().CreateFromZipAsync(settings, Request(zip, folder, primary: true),
            new SyncProgress<CreateProgress>(p => steps.Add(p.Step)));

        Assert.True(File.Exists(Path.Combine(folder, "buildID64.txt")));
        Assert.True(File.Exists(Path.Combine(folder, "GameData", "Squad", "file2.txt")));
        Assert.Equal("1.12.5", instance.Version);
        Assert.True(instance.IsPrimary);
        Assert.Equal(KspVersions.DefaultCompatibleVersions("1.12.5"), instance.CompatibleKspVersions);
        Assert.Same(instance, Assert.Single(settings.Instances));
        Assert.Same(instance, Assert.Single(_saved));
        Assert.Equal(Path.Combine(_root, "instances"), settings.InstanceParentFolder);
        Assert.Equal([CreateStep.CheckZip, CreateStep.Unpack, CreateStep.Prefix, CreateStep.Save, CreateStep.Done],
            steps.Distinct());
    }

    [Fact]
    public async Task NewPrimaryTakesOverTheFlag()
    {
        var old = new Instance("Old", Path.Combine(_root, "old"), "1.12.5") { IsPrimary = true };
        var settings = new AppSettings { Instances = [old] };
        var zip = new ZipService().GetVersionFromKspZip(MakeZip("ksp.zip"));

        await Creator().CreateFromZipAsync(settings, Request(zip, Path.Combine(_root, "new"), primary: true));

        Assert.False(old.IsPrimary);
    }

    [Fact]
    public async Task CancelDeletesTheHalfMadeFolder()
    {
        var settings = new AppSettings();
        var zip = new ZipService().GetVersionFromKspZip(MakeZip("ksp.zip", files: 200));
        var folder = Path.Combine(_root, "instances", "career");
        using var cancel = new CancellationTokenSource();
        var progress = new SyncProgress<CreateProgress>(p =>
        {
            if (p.Unpack is { FilesDone: >= 10 }) cancel.Cancel();
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Creator().CreateFromZipAsync(settings, Request(zip, folder), progress, cancel.Token));

        Assert.False(Directory.Exists(folder));
        Assert.Empty(settings.Instances);
        Assert.Empty(_saved);
    }

    [Fact]
    public async Task FailedSaveUndoesEverything()
    {
        var old = new Instance("Old", Path.Combine(_root, "old"), "1.12.5") { IsPrimary = true };
        var settings = new AppSettings { Instances = [old] };
        var zip = new ZipService().GetVersionFromKspZip(MakeZip("ksp.zip"));
        var folder = Path.Combine(_root, "instances", "career");

        await Assert.ThrowsAsync<IOException>(() => Creator((_, _) => throw new IOException("disk full"))
            .CreateFromZipAsync(settings, Request(zip, folder, primary: true)));

        Assert.False(Directory.Exists(folder));
        Assert.Same(old, Assert.Single(settings.Instances));
        Assert.True(old.IsPrimary);
        Assert.Null(settings.InstanceParentFolder);
    }

    [Fact]
    public async Task NonEmptyFolderIsRefusedAndLeftAlone()
    {
        var zip = new ZipService().GetVersionFromKspZip(MakeZip("ksp.zip"));
        var folder = Path.Combine(_root, "taken");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "mine.txt"), "keep");

        await Assert.ThrowsAsync<IOException>(() =>
            Creator().CreateFromZipAsync(new AppSettings(), Request(zip, folder)));

        Assert.True(File.Exists(Path.Combine(folder, "mine.txt")));
    }

    [Fact]
    public async Task ZipWithAnotherVersionIsRefused()
    {
        var path = MakeZip("ksp.zip", buildId: "02694");
        var zip = new KspZip(path, "KSP_linux", "1.12.5");
        var folder = Path.Combine(_root, "career");

        var error = await Assert.ThrowsAsync<InvalidDataException>(() =>
            Creator().CreateFromZipAsync(new AppSettings(), Request(zip, folder)));

        Assert.Contains("1.8.1", error.Message);
        Assert.False(Directory.Exists(folder));
    }

    [Fact]
    public async Task ZipEntriesOutsideTheFolderAreRefused()
    {
        var path = MakeZip("evil.zip");
        using (var archive = ZipFile.Open(path, ZipArchiveMode.Update))
            archive.CreateEntry("KSP_linux/../../escaped.txt");
        var zip = new ZipService().GetVersionFromKspZip(path);
        var folder = Path.Combine(_root, "deep", "career");

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            Creator().CreateFromZipAsync(new AppSettings(), Request(zip, folder)));

        Assert.False(File.Exists(Path.Combine(_root, "escaped.txt")));
        Assert.False(Directory.Exists(folder));
    }

    [Fact]
    public void DialogFillsNameFolderAndVersions()
    {
        var settings = new AppSettings { InstanceParentFolder = Path.Combine(_root, "instances") };
        settings.KspZips.Add(new ZipService().GetVersionFromKspZip(MakeZip("ksp.zip")));
        var vm = new NewInstanceDialogViewModel(settings, new ZipService(), Creator(), _ => { });

        Assert.Equal("1.12.5", vm.KspText);
        Assert.Equal("KSP 1.12.5", vm.Name);
        Assert.Equal("ksp-1.12.5", vm.FolderName);
        Assert.True(vm.IsPrimary);
        Assert.Equal(["All 1.8.x", "All 1.9.x", "All 1.10.x", "All 1.11.x", "All 1.12.x"], vm.VersionChips);
        Assert.True(vm.CreateCommand.CanExecute(null));

        vm.Name = "Career 1.12.5";
        Assert.Equal(Path.Combine(_root, "instances", "career-1.12.5"), vm.FolderPath);

        vm.Name = " ";
        Assert.NotNull(vm.NameError);
        Assert.False(vm.CreateCommand.CanExecute(null));
    }

    [Fact]
    public void DialogWithoutZipsShowsTheEmptyState()
    {
        var vm = new NewInstanceDialogViewModel(new AppSettings(), new ZipService(), Creator(), _ => { });

        Assert.True(vm.HasNoZips);
        Assert.Null(vm.Versions);
        Assert.Equal("–", vm.KspText);
        Assert.False(vm.CreateCommand.CanExecute(null));
    }

    [Fact]
    public void EditedVersionsShowInTheChips()
    {
        var settings = new AppSettings();
        settings.KspZips.Add(new ZipService().GetVersionFromKspZip(MakeZip("ksp.zip")));
        var vm = new NewInstanceDialogViewModel(settings, new ZipService(), Creator(), _ => { });

        vm.Versions!.Groups.Single(g => g.Prefix == "1.8").AllowAll = false;
        vm.Versions.Groups.Single(g => g.Prefix == "1.9").Versions.Single(v => v.Version == "1.9.1").IsChecked = true;

        Assert.Equal(["All 1.9.x", "1.9.1", "All 1.10.x", "All 1.11.x", "All 1.12.x"], vm.VersionChips);
    }

    [Fact]
    public async Task DialogCreatesAndFinishesWithTheInstance()
    {
        var settings = new AppSettings { InstanceParentFolder = Path.Combine(_root, "instances") };
        settings.KspZips.Add(new ZipService().GetVersionFromKspZip(MakeZip("ksp.zip")));
        Instance? finished = null;
        var vm = new NewInstanceDialogViewModel(settings, new ZipService(), Creator(), i => finished = i);

        await vm.CreateCommand.ExecuteAsync(null);

        Assert.NotNull(finished);
        Assert.Equal("KSP 1.12.5", finished.Name);
        Assert.True(Directory.Exists(Path.Combine(_root, "instances", "ksp-1.12.5", "GameData")));
    }

    private sealed class SyncProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}
