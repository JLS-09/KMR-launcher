using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using KMRLauncherMvvm.Models;
using LibGit2Sharp;

namespace KMRLauncherMvvm.Services.Api;

public class ModApiService(HttpClient http, ModListService modList) : IModApiService
{
    public async Task GetAllModsAsync(IProgress<ModFetchProgress>? progress = null)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };
        
        var refs = Repository.ListRemoteReferences("https://github.com/KSP-CKAN/CKAN-meta.git");
        var latestCommitHash = refs.First(r => r.CanonicalName == "refs/heads/master").TargetIdentifier;
        
        var basePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appFolder = Path.Combine(basePath, "kmrLauncher/mods.json");
        
        Directory.CreateDirectory(Path.Combine(basePath, "kmrLauncher"));

        if (File.Exists(appFolder))
        {
            progress?.Report(new ModFetchProgress
            {
                TotalMods =  1,
                ModsReceived =  1,
                CurrentModName = "Loading cache",
                IsCache = true
            });
            try
            {
                var json = await File.ReadAllTextAsync(appFolder);
                var modsCache = JsonSerializer.Deserialize<ModsCache>(json, options);
                if (modsCache is not null && modsCache.CurrentCommitHash == latestCommitHash ) 
                    modList.Mods = new ObservableCollection<Mod>(modsCache.Mods);
                return;
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"Path: {ex.Path}, LineNumber: {ex.LineNumber}");
                Console.WriteLine(ex.Message);
            }
        }

        using var response = await http.GetAsync("api/mods/all", HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        var total = 0;
        if (response.Headers.TryGetValues("X-Total-Count", out var values))
            int.TryParse(values.First(), out total);

        var mods = new List<Mod>();
        var received = 0;

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream);

        while (await reader.ReadLineAsync() is { } line)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            Mod? mod;
            try
            {
                mod = JsonSerializer.Deserialize<Mod>(line, options);
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"Path: {ex.Path}, LineNumber: {ex.LineNumber}");
                Console.WriteLine(ex.Message);
                throw;
            }
            
            if (mod != null)
            {
                mod.Versions.Sort((x, y) =>
                {
                    if (x.ReleaseDate.HasValue && y.ReleaseDate.HasValue)
                        return DateTime.Compare(y.ReleaseDate.Value, x.ReleaseDate.Value);

                    if (x.ReleaseDate.HasValue) return -1;
                    if (y.ReleaseDate.HasValue) return 1;

                    return Helpers.CompareVersions(x.Version, y.Version);
                });
                mods.Add(mod);
            }
            
            received++;
            if (total > 0)
                progress?.Report(new ModFetchProgress
                {
                    TotalMods =  total,
                    ModsReceived =  received,
                    CurrentModName = mod?.Name
                });
        }
        
        var cache = new ModsCache{Mods = mods, CurrentCommitHash = latestCommitHash};
        var modlistJson = JsonSerializer.Serialize(cache, options);
        await File.WriteAllTextAsync(appFolder, modlistJson);

        modList.Mods = new ObservableCollection<Mod>(mods);
    }
}