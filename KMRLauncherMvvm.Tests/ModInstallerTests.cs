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
                var entry = zip.CreateEntry(path);
                if (path.EndsWith('/')) continue; // a folder entry
                using var writer = new StreamWriter(entry.Open());
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
    public void Ckan_files_inside_the_zip_are_not_installed()
    {
        var zip = Zip("MyMod/a.cfg", "MyMod/MyMod.ckan");

        Assert.Equal(["GameData/MyMod/a.cfg"], Targets(WithInstall("MyMod"), zip));
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
        // CKAN also allows Missions and Ships/Script
        Assert.True(ModInstaller.BuildPlan(
            WithInstall("X", new InstallDirective { Find = "Craft", InstallTo = "Missions" }), zip)[0].MayCreateDirectories);
        Assert.True(ModInstaller.BuildPlan(
            WithInstall("X", new InstallDirective { Find = "Craft", InstallTo = "Ships/Script" }), zip)[0]
            .MayCreateDirectories);
    }

    [Fact]
    public void Find_with_a_path_matches_the_end_of_a_path()
    {
        // Advanced Jet Engine v2.20.1: { "find": "GameData/AJE", "install_to": "GameData" }
        var zip = Zip("GameData/", "GameData/AJE/", "GameData/AJE/B9.cfg", "GameData/AJE/Plugins/AJE.dll");
        var mod = WithInstall("AdvancedJetEngine",
            new InstallDirective { Find = "GameData/AJE", InstallTo = "GameData" });

        Assert.Equal(["GameData/AJE/B9.cfg", "GameData/AJE/Plugins/AJE.dll"], Targets(mod, zip));

        var nested = Zip("AJE-2.20/GameData/AJE/B9.cfg", "AJE-2.20/Extras/AJE/x.cfg");
        Assert.Equal(["GameData/AJE/B9.cfg"], Targets(mod, nested));
    }

    [Fact]
    public void File_find_and_find_regexp_ignore_case()
    {
        var zip = Zip("release/gamedata/MyMod/a.cfg");

        Assert.Equal(["GameData/MyMod/a.cfg"],
            Targets(WithInstall("X", new InstallDirective { Find = "mymod", InstallTo = "GameData" }), zip));
        Assert.Equal(["GameData/MyMod/a.cfg"],
            Targets(WithInstall("X", new InstallDirective { File = "Release/GameData/MyMod", InstallTo = "GameData" }), zip));
        Assert.Equal(["GameData/MyMod/a.cfg"],
            Targets(WithInstall("X", new InstallDirective { FindRegexp = "MYMOD$", InstallTo = "GameData" }), zip));
    }

    [Fact]
    public void Find_installs_every_match_that_starts_earliest()
    {
        var zip = Zip("A/Foo/a.cfg", "B/Foo/b.cfg", "C/Deep/Foo/c.cfg");
        var mod = WithInstall("X", new InstallDirective { Find = "Foo", InstallTo = "GameData" });

        Assert.Equal(["GameData/Foo/a.cfg", "GameData/Foo/b.cfg"], Targets(mod, zip));
    }

    [Fact]
    public void Find_regexp_installs_every_match_not_inside_another()
    {
        var zip = Zip("Pack/Textures-HR/a.dds", "Pack/Textures-HR/Textures-Extra/b.dds", "Pack/Textures-LR/c.dds",
            "Pack/Other/d.dds");
        var mod = WithInstall("X", new InstallDirective { FindRegexp = "Textures-", InstallTo = "GameData/Pack" });

        Assert.Equal(
            ["GameData/Pack/Textures-HR/Textures-Extra/b.dds", "GameData/Pack/Textures-HR/a.dds",
                "GameData/Pack/Textures-LR/c.dds"],
            Targets(mod, zip));
    }

    [Fact]
    public void A_matched_game_folder_is_not_installed_inside_itself_except_to_GameRoot()
    {
        var zip = Zip("Foo-1.0/GameData/Foo/a.cfg", "Foo-1.0/Ships/VAB/My.craft");

        Assert.Equal(["GameData/Foo/a.cfg"],
            Targets(WithInstall("X", new InstallDirective { File = "Foo-1.0/GameData", InstallTo = "GameData" }), zip));
        Assert.Equal(["GameData/Foo/a.cfg"],
            Targets(WithInstall("X", new InstallDirective { File = "Foo-1.0/GameData", InstallTo = "GameRoot" }), zip));
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
    public void A_directive_whose_files_another_directive_already_installs_is_fine()
    {
        // ImprovedTreeEnginePlacement v1.4.0
        var zip = Zip("ImprovedTreeEnginePlacement/a.cfg", "ImprovedTreeEnginePlacement/Extras/b.cfg");
        var mod = WithInstall("X",
            new InstallDirective { Find = "ImprovedTreeEnginePlacement", InstallTo = "GameData" },
            new InstallDirective
                { Find = "ImprovedTreeEnginePlacement/Extras", InstallTo = "GameData/ImprovedTreeEnginePlacement" });

        Assert.Equal(["GameData/ImprovedTreeEnginePlacement/Extras/b.cfg", "GameData/ImprovedTreeEnginePlacement/a.cfg"],
            Targets(mod, zip));
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