using KMRLauncherMvvm.Services;
using KMRLauncherMvvm.ViewModels;

namespace KMRLauncherMvvm.Tests;

public class SyncStatusTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "kmr-sync-" + Guid.NewGuid().ToString("N"));
    private string File => Path.Combine(_folder, "sync.json");

    private static readonly DateTimeOffset Now = new(2026, 11, 21, 20, 0, 0, TimeSpan.FromHours(1));

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
    }

    [Fact]
    public void NothingSavedMeansNeverFetched()
    {
        var sync = new SyncStatusService(File);

        Assert.Null(sync.Status.LastFetch);
        Assert.False(sync.Status.LastAttemptFailed);
        Assert.Equal("synced –", StatusBarViewModel.SyncTexts(sync, Now).Text);
    }

    [Fact]
    public void SuccessIsSavedAndReadBack()
    {
        new SyncStatusService(File).RecordSuccess("7d01e4b0123456789", Now.AddMinutes(-12));

        var sync = new SyncStatusService(File);
        Assert.Equal(Now.AddMinutes(-12), sync.Status.LastFetch);
        Assert.Equal("7d01e4b0123456789", sync.Status.LastCommit);
        Assert.Equal("synced 12 min ago", StatusBarViewModel.SyncTexts(sync, Now).Text);
    }

    [Fact]
    public void FailureKeepsTheLastSuccess()
    {
        var sync = new SyncStatusService(File);
        sync.RecordSuccess("abcdef0123", Now.AddHours(-3));
        sync.RecordFailure("Couldn't reach github.com", Now.AddMinutes(-1));

        var reloaded = new SyncStatusService(File);
        Assert.True(reloaded.Status.LastAttemptFailed);
        Assert.Equal(Now.AddHours(-3), reloaded.Status.LastFetch);
        Assert.Equal("abcdef0123", reloaded.Status.LastCommit);

        var (text, hint) = StatusBarViewModel.SyncTexts(reloaded, Now);
        Assert.Equal("sync failed", text);
        Assert.Contains("Couldn't reach github.com", hint);
        Assert.Contains("abcdef0", hint);
    }

    [Fact]
    public void SuccessAfterFailureClearsTheFailure()
    {
        var sync = new SyncStatusService(File);
        sync.RecordFailure("offline", Now.AddMinutes(-5));
        sync.RecordSuccess("abcdef0123", Now);

        Assert.False(sync.Status.LastAttemptFailed);
        Assert.Null(sync.Status.LastError);
    }

    [Fact]
    public void BrokenFileIsIgnored()
    {
        Directory.CreateDirectory(_folder);
        System.IO.File.WriteAllText(File, "{ not json");

        Assert.Null(new SyncStatusService(File).Status.LastFetch);
    }

    [Theory]
    [InlineData(30, "just now")]
    [InlineData(12 * 60, "12 min ago")]
    [InlineData(5 * 3600, "5 h ago")]
    [InlineData(47 * 3600, "47 h ago")]
    [InlineData(3 * 86400, "3 days ago")]
    public void TimeAgo(int secondsAgo, string expected) =>
        Assert.Equal(expected, SyncStatusService.TimeAgo(Now.AddSeconds(-secondsAgo), Now));
}
