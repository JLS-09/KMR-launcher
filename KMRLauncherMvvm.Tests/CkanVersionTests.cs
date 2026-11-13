using KMRLauncherMvvm.Services;

namespace KMRLauncherMvvm.Tests;

// Spec "version" -> "Version ordering"
public class CkanVersionTests
{
    [Theory]
    [InlineData("1.2", "1.2.1")]
    [InlineData("1.2.1", "1.3")]
    [InlineData("1.9", "1.10")]
    [InlineData("1.2-beta2", "1.2-beta10")]
    [InlineData("1.0", "1.0-pre")] // per spec the shorter string is older, even for "pre"
    [InlineData("1.0a", "1.0.")] // letters sort before non-letters
    [InlineData("v1.9", "v1.10")]
    [InlineData("20231015123456", "20231015123457")] // longer than an int
    [InlineData("5.0", "1:0.1")] // epoch wins
    public void Older_version_compares_lower(string older, string newer)
    {
        Assert.True(CkanVersion.Compare(older, newer) < 0);
        Assert.True(CkanVersion.Compare(newer, older) > 0);
    }

    [Theory]
    [InlineData("1.02", "1.2")]
    [InlineData("0:1.2", "1.2")]
    [InlineData("1.2", "1.2")]
    public void Equal_versions_compare_zero(string a, string b)
    {
        Assert.Equal(0, CkanVersion.Compare(a, b));
    }
}
