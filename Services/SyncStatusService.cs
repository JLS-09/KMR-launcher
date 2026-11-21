using System;
using System.IO;
using System.Text.Json;
using KMRLauncherMvvm.Models;

namespace KMRLauncherMvvm.Services;

public class SyncStatusService
{
    public static string DefaultFile => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "kmrLauncher", "sync.json");

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    private readonly string _file;

    public SyncStatus Status { get; private set; }
    public bool IsFetching { get; private set; }

    public event Action? Changed;

    public SyncStatusService() : this(DefaultFile)
    {
    }

    public SyncStatusService(string file)
    {
        _file = file;
        Status = Load(file);
    }

    public void SetFetching(bool fetching)
    {
        IsFetching = fetching;
        Changed?.Invoke();
    }

    public void RecordSuccess(string? commit, DateTimeOffset? at = null)
    {
        Status = new SyncStatus { LastFetch = at ?? DateTimeOffset.Now, LastCommit = commit };
        Save();
        Changed?.Invoke();
    }

    public void RecordFailure(string error, DateTimeOffset? at = null)
    {
        Status = new SyncStatus
        {
            LastFetch = Status.LastFetch,
            LastCommit = Status.LastCommit,
            LastFailure = at ?? DateTimeOffset.Now,
            LastError = error
        };
        Save();
        Changed?.Invoke();
    }

    public static string TimeAgo(DateTimeOffset time, DateTimeOffset now)
    {
        var elapsed = now - time;
        if (elapsed < TimeSpan.FromMinutes(1)) return "just now";
        if (elapsed < TimeSpan.FromHours(1)) return $"{(int)elapsed.TotalMinutes} min ago";
        if (elapsed < TimeSpan.FromHours(48)) return $"{(int)elapsed.TotalHours} h ago";
        return $"{(int)elapsed.TotalDays} days ago";
    }

    private static SyncStatus Load(string file)
    {
        try
        {
            return File.Exists(file)
                ? JsonSerializer.Deserialize<SyncStatus>(File.ReadAllText(file), Options) ?? new SyncStatus()
                : new SyncStatus();
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException)
        {
            return new SyncStatus();
        }
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
            AtomicFile.WriteAllText(_file, JsonSerializer.Serialize(Status, Options));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }
}
