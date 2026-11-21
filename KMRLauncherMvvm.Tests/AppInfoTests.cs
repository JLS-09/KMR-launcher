using KMRLauncherMvvm.Services;

namespace KMRLauncherMvvm.Tests;

public class AppInfoTests
{
    [Theory]
    [InlineData("1.0.0+1d63bca946a1edf23f923fa50fd742c5fb491799", "dev build · 1d63bca")]
    [InlineData("0.5.0+1d63bca946a1edf23f923fa50fd742c5fb491799", "0.5.0 · 1d63bca")]
    [InlineData("0.5.0", "0.5.0")]
    [InlineData("1.0.0", "dev build")]
    [InlineData(null, "dev build")]
    public void VersionText(string? informational, string expected) =>
        Assert.Equal(expected, AppInfo.Describe(informational));
}
