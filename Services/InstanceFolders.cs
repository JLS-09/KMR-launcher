using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using KMRLauncherMvvm.Models;

namespace KMRLauncherMvvm.Services;

public static class InstanceFolders
{
    public static readonly string DefaultParent = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "KMR", "instances");

    public static string ParentFor(AppSettings settings) =>
        string.IsNullOrWhiteSpace(settings.InstanceParentFolder) ? DefaultParent : settings.InstanceParentFolder;

    public static string Slug(string name)
    {
        var builder = new StringBuilder();
        foreach (var c in name.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c) || c is '.' or '_') builder.Append(c);
            else if ((char.IsWhiteSpace(c) || c == '-') && builder.Length > 0 && builder[^1] != '-') builder.Append('-');
        }

        var slug = builder.ToString().Trim('-', '.');
        return slug.Length == 0 ? "instance" : slug;
    }

    public static string FreeFolder(string parent, string name, IEnumerable<Instance> instances) =>
        FreeFolder(parent, name, instances, path => Directory.Exists(path) || File.Exists(path));

    public static string FreeFolder(string parent, string name, IEnumerable<Instance> instances,
        Func<string, bool> exists)
    {
        var taken = instances.Select(i => Path.GetFullPath(i.RootPath)).ToHashSet();
        var slug = Slug(name);
        var candidate = Path.Combine(parent, slug);
        for (var n = 2; exists(candidate) || taken.Contains(Path.GetFullPath(candidate)); n++)
            candidate = Path.Combine(parent, $"{slug}-{n}");
        return candidate;
    }
}
