using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using KMRLauncherMvvm.Exceptions;
using KMRLauncherMvvm.Models;

namespace KMRLauncherMvvm.Services;

public interface IModDownloader
{
    string? FindCached(ModVersion mod);

    Task<string> DownloadAsync(ModVersion mod, Action<long, long?>? onProgress, CancellationToken cancellationToken);
}

public class ModDownloader(HttpClient http, string cacheFolder) : IModDownloader
{
    private static readonly TimeSpan StallTimeout = TimeSpan.FromSeconds(30);

    public static string DefaultCacheFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "kmrLauncher/cache/downloads");

    public static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("KMRLauncher/1.0");
        return client;
    }

    public string? FindCached(ModVersion mod)
    {
        var path = CachePath(mod);
        return File.Exists(path) ? path : null;
    }

    public async Task<string> DownloadAsync(ModVersion mod, Action<long, long?>? onProgress,
        CancellationToken cancellationToken)
    {
        var urls = mod.Download?.Where(u => !string.IsNullOrWhiteSpace(u)).ToList() ?? [];
        if (urls.Count == 0)
            throw new ModInstallException($"{mod.Identifier} {mod.Version} has no download URL.");

        Directory.CreateDirectory(cacheFolder);
        var finalPath = CachePath(mod);
        var partPath = finalPath + ".part";
        var errors = new List<string>();

        foreach (var url in urls)
        {
            try
            {
                await DownloadFile(url, partPath, mod, onProgress, cancellationToken);
                File.Move(partPath, finalPath, overwrite: true);
                return finalPath;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                TryDelete(partPath);
                throw;
            }
            catch (Exception e) when (e is HttpRequestException or IOException or OperationCanceledException
                                          or HashMismatchException)
            {
                TryDelete(partPath);
                errors.Add($"{Host(url)}: {Describe(e)}");
            }
        }

        throw new ModInstallException(
            $"Couldn't download {mod.Identifier} {mod.Version}. {string.Join("; ", errors)}");
    }

    private async Task DownloadFile(string url, string partPath, ModVersion mod, Action<long, long?>? onProgress,
        CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength ?? mod.DownloadSize;
        var (expected, algorithm) = ExpectedHash(mod);
        using var hash = algorithm is null ? null : IncrementalHash.CreateHash(algorithm.Value);

        await using (var source = await response.Content.ReadAsStreamAsync(cancellationToken))
        await using (var target = new FileStream(partPath, FileMode.Create, FileAccess.Write, FileShare.None,
                         bufferSize: 81920, useAsync: true))
        {
            var buffer = new byte[81920];
            long done = 0;
            onProgress?.Invoke(0, total);

            while (true)
            {
                using var stall = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                stall.CancelAfter(StallTimeout);

                var read = await source.ReadAsync(buffer, stall.Token);
                if (read == 0) break;

                await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                hash?.AppendData(buffer, 0, read);
                done += read;
                onProgress?.Invoke(done, total);
            }
        }

        if (hash is null) return;
        var actual = Convert.ToHexString(hash.GetHashAndReset());
        if (!actual.Equals(expected, StringComparison.OrdinalIgnoreCase))
            throw new HashMismatchException();
    }

    private static (string? Expected, HashAlgorithmName? Algorithm) ExpectedHash(ModVersion mod)
    {
        if (!string.IsNullOrWhiteSpace(mod.DownloadHash?.Sha256))
            return (mod.DownloadHash.Sha256.Trim(), HashAlgorithmName.SHA256);
        if (!string.IsNullOrWhiteSpace(mod.DownloadHash?.Sha1))
            return (mod.DownloadHash.Sha1.Trim(), HashAlgorithmName.SHA1);
        return (null, null);
    }

    public string CachePath(ModVersion mod)
    {
        var (expected, _) = ExpectedHash(mod);
        var key = expected ?? Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(mod.Download?.FirstOrDefault() ?? "")));
        var name = $"{key[..8].ToUpperInvariant()}-{Safe(mod.Identifier)}-{Safe(mod.Version)}.zip";
        return Path.Combine(cacheFolder, name);
    }

    private static string Safe(string text) =>
        new(text.Select(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_' ? c : '_').ToArray());

    private static string Host(string url) => Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Host : url;

    private static string Describe(Exception e) => e switch
    {
        HttpRequestException { StatusCode: { } status } => $"server answered {(int)status} {status}",
        HttpRequestException http => http.InnerException?.Message ?? http.Message,
        HashMismatchException => "the downloaded file doesn't match its download_hash",
        OperationCanceledException => "the download stopped responding",
        _ => e.Message
    };

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
    }

    private class HashMismatchException : Exception;
}