using System.Net;
using System.Security.Cryptography;
using KMRLauncherMvvm.Exceptions;
using KMRLauncherMvvm.Models;
using KMRLauncherMvvm.Services;
using static KMRLauncherMvvm.Tests.TestData;

namespace KMRLauncherMvvm.Tests;

public class ModDownloaderTests : IDisposable
{
    private static readonly byte[] Content = "fake zip content"u8.ToArray();
    private static readonly string Sha256 = Convert.ToHexString(SHA256.HashData(Content));
    private static readonly string Sha1 = Convert.ToHexString(SHA1.HashData(Content));

    private readonly string _cache = Path.Combine(Path.GetTempPath(), "kmr-downloader-" + Guid.NewGuid());

    public void Dispose()
    {
        if (Directory.Exists(_cache)) Directory.Delete(_cache, true);
    }

    // Answers per URL: a status code, or the content with 200
    private class FakeHandler(Dictionary<string, Func<HttpResponseMessage>> answers) : HttpMessageHandler
    {
        public List<string> Requested { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var url = request.RequestUri!.ToString();
            Requested.Add(url);
            return Task.FromResult(answers.TryGetValue(url, out var answer)
                ? answer()
                : new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }

    private static Func<HttpResponseMessage> Ok(byte[] content) =>
        () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(content) };

    private (ModDownloader Downloader, FakeHandler Handler) Create(Dictionary<string, Func<HttpResponseMessage>> answers)
    {
        var handler = new FakeHandler(answers);
        return (new ModDownloader(new HttpClient(handler), _cache), handler);
    }

    private static ModVersion Downloadable(string version = "1.0", string? sha256 = null, string? sha1 = null,
        params string[] urls)
    {
        var mod = Version("Foo", version);
        mod.Download = [.. urls];
        mod.DownloadHash = sha256 is null && sha1 is null ? null : new DownloadHash { Sha256 = sha256, Sha1 = sha1 };
        return mod;
    }

    [Fact]
    public async Task Downloads_verifies_and_caches()
    {
        var (downloader, _) = Create(new() { ["https://a.test/foo.zip"] = Ok(Content) });
        var mod = Downloadable(sha256: Sha256.ToLowerInvariant(), urls: "https://a.test/foo.zip");
        var reports = new List<(long, long?)>();

        Assert.Null(downloader.FindCached(mod));
        var path = await downloader.DownloadAsync(mod, (done, total) => reports.Add((done, total)), CancellationToken.None);

        Assert.Equal(Content, await File.ReadAllBytesAsync(path));
        Assert.Equal(path, downloader.FindCached(mod));
        Assert.StartsWith(Sha256[..8] + "-Foo-1.0", Path.GetFileName(path));
        Assert.Equal((Content.Length, (long?)Content.Length), reports[^1]);
        Assert.Empty(Directory.GetFiles(_cache, "*.part"));
    }

    [Fact]
    public async Task Next_mirror_is_tried_after_an_error_or_a_wrong_hash()
    {
        var (downloader, handler) = Create(new()
        {
            ["https://wrong.test/foo.zip"] = Ok("something else"u8.ToArray()),
            ["https://good.test/foo.zip"] = Ok(Content)
        });
        var mod = Downloadable(sha256: Sha256,
            urls: ["https://missing.test/foo.zip", "https://wrong.test/foo.zip", "https://good.test/foo.zip"]);

        var path = await downloader.DownloadAsync(mod, null, CancellationToken.None);

        Assert.Equal(3, handler.Requested.Count);
        Assert.Equal(Content, await File.ReadAllBytesAsync(path));
    }

    [Fact]
    public async Task All_mirrors_failing_gives_one_message_and_leaves_nothing_in_the_cache()
    {
        var (downloader, _) = Create(new() { ["https://wrong.test/foo.zip"] = Ok("something else"u8.ToArray()) });
        var mod = Downloadable(sha256: Sha256, urls: ["https://missing.test/foo.zip", "https://wrong.test/foo.zip"]);

        var error = await Assert.ThrowsAsync<ModInstallException>(() =>
            downloader.DownloadAsync(mod, null, CancellationToken.None));

        Assert.Contains("Foo 1.0", error.Message);
        Assert.Contains("missing.test: server answered 404 NotFound", error.Message);
        Assert.Contains("wrong.test: the downloaded file doesn't match its download_hash", error.Message);
        Assert.Null(downloader.FindCached(mod));
        Assert.Empty(Directory.GetFiles(_cache));
    }

    [Fact]
    public async Task Sha1_is_used_when_there_is_no_sha256_and_no_hash_is_accepted()
    {
        var (downloader, _) = Create(new() { ["https://a.test/foo.zip"] = Ok(Content) });

        await downloader.DownloadAsync(Downloadable(sha1: Sha1, urls: "https://a.test/foo.zip"), null,
            CancellationToken.None);
        await Assert.ThrowsAsync<ModInstallException>(() =>
            downloader.DownloadAsync(Downloadable("2.0", sha1: new string('0', 40), urls: "https://a.test/foo.zip"),
                null, CancellationToken.None));
        await downloader.DownloadAsync(Downloadable("3.0", urls: "https://a.test/foo.zip"), null,
            CancellationToken.None);
    }

    [Fact]
    public async Task No_download_url_fails()
    {
        var (downloader, _) = Create([]);

        await Assert.ThrowsAsync<ModInstallException>(() =>
            downloader.DownloadAsync(Downloadable(), null, CancellationToken.None));
    }

    [Fact]
    public async Task Cancelling_throws_and_leaves_no_part_file()
    {
        var (downloader, _) = Create(new() { ["https://a.test/foo.zip"] = Ok(Content) });
        using var cancel = new CancellationTokenSource();
        await cancel.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            downloader.DownloadAsync(Downloadable(urls: "https://a.test/foo.zip"), null, cancel.Token));
        Assert.Empty(Directory.GetFiles(_cache));
    }

    [Fact]
    public void Cache_file_name_is_safe_for_epochs_and_odd_versions()
    {
        var (downloader, _) = Create([]);
        var mod = Downloadable("1:2.0/beta", sha256: Sha256, urls: "https://a.test/foo.zip");

        Assert.Equal($"{Sha256[..8]}-Foo-1_2.0_beta.zip", Path.GetFileName(downloader.CachePath(mod)));
    }

    [Fact]
    public void Parallel_downloads_setting_defaults_to_4()
    {
        var settings = System.Text.Json.JsonSerializer.Deserialize<AppSettings>("{}")!;

        Assert.Equal(4, settings.MaxParallelDownloads);
    }
}