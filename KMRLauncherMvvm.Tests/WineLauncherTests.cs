using System.Text.Json;
using KMRLauncherMvvm.Models;
using KMRLauncherMvvm.Services;

namespace KMRLauncherMvvm.Tests;

public class WineLauncherTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "kmr-path-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
    }

    private string MakeFile(string folder, string name, bool executable)
    {
        var dir = Path.Combine(_folder, folder);
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, name);
        File.WriteAllText(path, "#!/bin/sh\n");
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(path, executable
                ? UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                : UnixFileMode.UserRead | UnixFileMode.UserWrite);
        return path;
    }

    [Theory]
    [InlineData(WineDebugLevel.Off, "-all")]
    [InlineData(WineDebugLevel.ErrorsOnly, "-all,err+all")]
    [InlineData(WineDebugLevel.WineDefault, null)]
    public void DebugLevelsMapToWinedebug(WineDebugLevel level, string? expected) =>
        Assert.Equal(expected, WineLauncher.DebugValue(level));

    [Fact]
    public void FindOnPathTakesTheFirstExecutableMatch()
    {
        MakeFile("a", "wine", executable: false);
        var expected = MakeFile("b", "wine", executable: true);
        MakeFile("c", "wine", executable: true);
        var path = string.Join(':', Path.Combine(_folder, "a"), Path.Combine(_folder, "b"), Path.Combine(_folder, "c"));

        Assert.Equal(expected, WineLauncher.FindOnPath("wine", path));
    }

    [Fact]
    public void FindOnPathReturnsNullWhenMissing()
    {
        MakeFile("a", "wine", executable: true);

        Assert.Null(WineLauncher.FindOnPath("umu-run", Path.Combine(_folder, "a")));
        Assert.Null(WineLauncher.FindOnPath("wine", ""));
    }

    [Fact]
    public void WineDebugIsSavedAsTextAndDefaultsToErrorsOnly()
    {
        var settings = new AppSettings();
        Assert.Equal(WineDebugLevel.ErrorsOnly, settings.WineDebug);

        settings.WineDebug = WineDebugLevel.WineDefault;
        var json = JsonSerializer.Serialize(settings);

        Assert.Contains("\"WineDebug\":\"WineDefault\"", json);
        Assert.Equal(WineDebugLevel.WineDefault, JsonSerializer.Deserialize<AppSettings>(json)!.WineDebug);
        Assert.Equal(WineDebugLevel.ErrorsOnly, JsonSerializer.Deserialize<AppSettings>("{}")!.WineDebug);
    }
}
