using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using KMRLauncherMvvm.Models;

namespace KMRLauncherMvvm.Services;

public class InstanceService
{
    private readonly string _file;

    public InstanceService(string file)
    {
        _file = file;
        if (!File.Exists(file))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            SettingsService.Write(new AppSettings(), file);
        }

        Settings = SettingsService.Load(file);
    }

    public AppSettings Settings { get; }
    public ObservableCollection<Instance> Instances => Settings.Instances;
    public IEnumerable<Instance> Usable => Settings.UsableInstances;
    public Instance? Primary => Usable.FirstOrDefault(i => i.IsPrimary) ?? Usable.FirstOrDefault();

    public event Action? Changed;

    public void Save(Instance? changed = null, bool modsChanged = false)
    {
        SettingsService.Write(Settings, _file, changed, modsChanged);
        Changed?.Invoke();
    }

    public void MakePrimary(Instance primary)
    {
        foreach (var instance in Instances) instance.IsPrimary = instance == primary;
        Save();
    }

    public void Forget(Instance instance)
    {
        var index = Instances.IndexOf(instance);
        if (index < 0) return;

        Instances.RemoveAt(index);
        try
        {
            Save();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Instances.Insert(index, instance);
            throw;
        }
    }

    public (Instance? Found, string? Error) Relocate(Instance missing, string folder)
    {
        var result = InstanceStore.Relocate(Settings, missing, folder);
        if (result.Found is not null) Save();
        return result;
    }

    public bool OwnsPrefix(Instance instance) =>
        WinePrefixes.IsManaged(instance.PrefixPath) &&
        !Instances.Any(i => i != instance && WinePrefixes.SamePath(i.PrefixPath, instance.PrefixPath));

    public async Task DeleteAsync(Instance instance)
    {
        var prefix = instance.PrefixPath;
        var deletePrefix = OwnsPrefix(instance);

        await Task.Run(() =>
        {
            if (Directory.Exists(instance.RootPath)) Directory.Delete(instance.RootPath, true);
            if (deletePrefix && Directory.Exists(prefix)) Directory.Delete(prefix, true);
        });

        Instances.Remove(instance);
        Save();
    }

    public void RecordPlay(Instance instance, DateTime started, DateTime exited)
    {
        instance.PlayTime += (int)(exited - started).TotalSeconds;
        instance.LastPlayed = exited;
        Save(instance);
    }
}
