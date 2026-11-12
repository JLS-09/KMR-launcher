using System.Collections.Generic;
using System.Linq;

namespace KMRLauncherMvvm.Models;

public sealed class GameVersionCriteria
{
    public string InstanceVersion { get; }

    public IReadOnlyList<string> ExactVersions { get; }

    public IReadOnlyList<string> MinorPrefixes { get; }

    public GameVersionCriteria(string instanceVersion, IEnumerable<string> compatibleVersions)
    {
        InstanceVersion = KspVersions.ToFullVersion(instanceVersion);

        List<string> exact = [InstanceVersion];
        List<string> prefixes = [];

        foreach (var entry in compatibleVersions.Select(v => v.Trim()).Where(v => v.Length > 0))
        {
            if (KspVersions.IsMinorPrefix(entry))
                prefixes.Add(entry);
            else
                exact.Add(KspVersions.ToFullVersion(entry));
        }

        ExactVersions = exact.Distinct().ToList();
        MinorPrefixes = prefixes.Distinct().ToList();
    }

    public static GameVersionCriteria FromInstance(Instance instance) =>
        new(instance.Version, instance.CompatibleKspVersions);

    public override string ToString() =>
        string.Join(", ", ExactVersions.Concat(MinorPrefixes.Select(p => $"{p}.x")));
}