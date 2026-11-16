using System.IO.Compression;
using KMRLauncherMvvm.Exceptions;
using KMRLauncherMvvm.Models;
using KMRLauncherMvvm.Services;
using static KMRLauncherMvvm.Tests.TestData;

namespace KMRLauncherMvvm.Tests;

public class ModInstallerTests
{
    // A zip with only file entries (no folder entries), like many mod zips
    private static ZipArchive Zip(params string[] paths)
    {
        var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var path in paths)
            {
                using var writer = new StreamWriter(zip.CreateEntry(path).Open());
                writer.Write(path);
            }
        }

        stream.Position = 0;
        return new ZipArchive(stream, ZipArchiveMode.Read);
    }

    private static ModVersion WithInstall(string identifier, params InstallDirective[] install)
    {
        var mod = Version(identifier, "1.0");
        mod.Install = install.Length == 0 ? null : [.. install];
        return mod;
    }

    private static List<string> Targets(ModVersion mod, ZipArchive zip) =>
        ModInstaller.BuildPlan(mod, zip).Select(s => s.TargetPath).Order(StringComparer.Ordinal).ToList();

    [Fact]
    public void Without_install_section_the_identifier_folder_goes_to_GameData()
    {
        var zip = Zip("MyMod-1.0/GameData/MyMod/Parts/a.cfg", "MyMod-1.0/GameData/MyMod/b.dll", "MyMod-1.0/README.md");

        Assert.Equal(["GameData/MyMod/Parts/a.cfg", "GameData/MyMod/b.dll"], Targets(WithInstall("MyMod"), zip));
    }

    [Fact]
    public void Find_takes_the_top_most_folder()
    {
        var zip = Zip("GameData/Foo/Foo/inner.cfg", "GameData/Foo/outer.cfg");
        var mod = WithInstall("X", new InstallDirective { Find = "Foo", InstallTo = "GameData" });

        Assert.Equal(["GameData/Foo/Foo/inner.cfg", "GameData/Foo/outer.cfg"], Targets(mod, zip));
    }

    [Fact]
    public void File_strips_the_leading_folders()
    {
        var zip = Zip("MyMods/KSP/Foo/a.cfg", "MyMods/KSP/Other/b.cfg");
        var mod = WithInstall("X", new InstallDirective { File = "MyMods/KSP/Foo", InstallTo = "GameData" });

        Assert.Equal(["GameData/Foo/a.cfg"], Targets(mod, zip));
    }

    [Fact]
    public void Find_regexp_matches_the_normalised_path()
    {
        var zip = Zip("Pack/Textures-HR/a.dds", "Pack/Textures-LR/b.dds");
        var mod = WithInstall("X", new InstallDirective { FindRegexp = "Textures-HR$", InstallTo = "GameData/Pack" });

        Assert.Equal(["GameData/Pack/Textures-HR/a.dds"], Targets(mod, zip));
    }

    [Fact]
    public void Find_only_matches_files_with_find_matches_files()
    {
        var zip = Zip("Release/Plugins/Foo.dll");
        var plain = WithInstall("X", new InstallDirective { Find = "Foo.dll", InstallTo = "GameData" });
        var files = WithInstall("X",
            new InstallDirective { Find = "Foo.dll", InstallTo = "GameData", FindMatchesFiles = true });

        Assert.Throws<ModInstallException>(() => ModInstaller.BuildPlan(plain, zip));
        Assert.Equal(["GameData/Foo.dll"], Targets(files, zip));
    }

    [Fact]
    public void As_renames_the_matched_folder_or_file()
    {
        var zip = Zip("Foo-master/a.cfg", "x/Readme.txt");
        var folder = WithInstall("X", new InstallDirective { File = "Foo-master", InstallTo = "GameData", As = "Foo" });
        var file = WithInstall("X",
            new InstallDirective { File = "x/Readme.txt", InstallTo = "GameData/Foo", As = "Foo.txt" });

        Assert.Equal(["GameData/Foo/a.cfg"], Targets(folder, zip));
        Assert.Equal(["GameData/Foo/Foo.txt"], Targets(file, zip));
    }

    [Fact]
    public void Filter_is_case_insensitive_and_filter_regexp_matches_the_full_path()
    {
        var zip = Zip("Foo/a.cfg", "Foo/thumbs.db", "Foo/Source/x.cs", "Foo/b.pdb");
        var mod = WithInstall("X", new InstallDirective
        {
            Find = "Foo", InstallTo = "GameData", Filter = ["Thumbs.db", "Source"], FilterRegexp = [@"\.pdb$"]
        });

        Assert.Equal(["GameData/Foo/a.cfg"], Targets(mod, zip));
    }

    [Fact]
    public void Include_only_and_include_only_regexp_keep_only_matching_files()
    {
        var zip = Zip("Foo/Settings.cfg", "Foo/Plugins/a.dll", "Foo/Parts/p.cfg", "Foo/readme.md");
        var mod = WithInstall("X", new InstallDirective
        {
            Find = "Foo", InstallTo = "GameData", IncludeOnly = ["settings.cfg", "Plugins"],
            IncludeOnlyRegexp = [@"\.md$"]
        });

        Assert.Equal(["GameData/Foo/Plugins/a.dll", "GameData/Foo/Settings.cfg", "GameData/Foo/readme.md"],
            Targets(mod, zip));
    }

    [Fact]
    public void Install_to_is_checked_against_the_spec()
    {
        var zip = Zip("Foo/a.cfg");

        Assert.Equal(["GameData/Foo/Plugins/Foo/a.cfg"],
            Targets(WithInstall("X", new InstallDirective { Find = "Foo", InstallTo = "GameData/Foo/Plugins" }), zip));
        Assert.Equal(["Foo/a.cfg"],
            Targets(WithInstall("X", new InstallDirective { Find = "Foo", InstallTo = "GameRoot" }), zip));

        foreach (var bad in new[] { "GameData/../Example", "Ships/Sub", "Foo", "", "/GameData/..", "GameData//x" })
        {
            var mod = WithInstall("X", new InstallDirective { Find = "Foo", InstallTo = bad });
            Assert.Throws<ModInstallException>(() => ModInstaller.BuildPlan(mod, zip));
        }
    }

    [Fact]
    public void Only_GameData_Tutorial_and_Scenarios_may_create_folders()
    {
        var zip = Zip("Craft/My.craft");

        Assert.True(ModInstaller.BuildPlan(
            WithInstall("X", new InstallDirective { Find = "Craft", InstallTo = "GameData" }), zip)[0].MayCreateDirectories);
        Assert.True(ModInstaller.BuildPlan(
            WithInstall("X", new InstallDirective { Find = "Craft", InstallTo = "Scenarios" }), zip)[0].MayCreateDirectories);
        Assert.False(ModInstaller.BuildPlan(
            WithInstall("X", new InstallDirective { File = "Craft/My.craft", InstallTo = "Ships/VAB" }), zip)[0]
            .MayCreateDirectories);
    }

    [Fact]
    public void Nothing_matched_fails_with_a_message()
    {
        var zip = Zip("Other/a.cfg");

        var error = Assert.Throws<ModInstallException>(() => ModInstaller.BuildPlan(WithInstall("MyMod"), zip));
        Assert.Contains("MyMod 1.0", error.Message);
        Assert.Contains("\"MyMod\"", error.Message);
    }

    [Fact]
    public void Unsafe_paths_and_double_targets_fail()
    {
        var escape = Zip("Foo/../../evil.dll");
        Assert.Throws<ModInstallException>(() =>
            ModInstaller.BuildPlan(WithInstall("X", new InstallDirective { File = "Foo", InstallTo = "GameData" }), escape));

        var twice = Zip("A/Foo/a.cfg", "B/Foo/a.cfg");
        var mod = WithInstall("X",
            new InstallDirective { File = "A/Foo", InstallTo = "GameData" },
            new InstallDirective { File = "B/Foo", InstallTo = "GameData" });
        Assert.Throws<ModInstallException>(() => ModInstaller.BuildPlan(mod, twice));
    }

    [Fact]
    public void Directive_needs_exactly_one_source()
    {
        var zip = Zip("Foo/a.cfg");
        var mod = WithInstall("X", new InstallDirective { File = "Foo", Find = "Foo", InstallTo = "GameData" });

        Assert.Throws<ModInstallException>(() => ModInstaller.BuildPlan(mod, zip));
    }

    [Fact]
    public void Include_only_accepts_a_single_string_in_json()
    {
        var directive = System.Text.Json.JsonSerializer.Deserialize<InstallDirective>(
            """{ "find": "Foo", "install_to": "GameData", "include_only": "Settings.cfg", "include_only_regexp": "x$" }""");

        Assert.Equal(["Settings.cfg"], directive!.IncludeOnly);
        Assert.Equal(["x$"], directive.IncludeOnlyRegexp);
    }
}