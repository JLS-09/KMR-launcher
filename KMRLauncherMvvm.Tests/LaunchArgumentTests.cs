using KMRLauncherMvvm.Models;

namespace KMRLauncherMvvm.Tests;

public class LaunchArgumentTests
{
    [Theory]
    [InlineData("", new string[0])]
    [InlineData("   ", new string[0])]
    [InlineData("-force-glcore", new[] { "-force-glcore" })]
    [InlineData("  -force-glcore   -popupwindow ", new[] { "-force-glcore", "-popupwindow" })]
    [InlineData("-logFile \"my log.txt\" -x", new[] { "-logFile", "my log.txt", "-x" })]
    [InlineData("\"\"", new[] { "" })]
    public void Arguments_are_split_on_spaces_outside_quotes(string input, string[] expected)
    {
        Assert.Equal(expected, Instance.SplitArguments(input));
    }
}