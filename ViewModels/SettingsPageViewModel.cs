using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KMRLauncherMvvm.Data;
using KMRLauncherMvvm.Models;
using KMRLauncherMvvm.Services;
using KMRLauncherMvvm.ViewModels.Settings;

namespace KMRLauncherMvvm.ViewModels;

public partial class SettingsPageViewModel : PageViewModel
{
    private readonly ZipService? _zipService;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasNoZips))]
    private IReadOnlyList<KspZipRow> _kspZips = [];

    [ObservableProperty] private string? _addZipError;
    [ObservableProperty] private string _parallelDownloads = "";

    public bool HasNoZips => KspZips.Count == 0;

    public SettingsPageViewModel(ZipService zipService) : this()
    {
        _zipService = zipService;
        LoadZips();
        ParallelDownloads = App.Settings.MaxParallelDownloads.ToString(CultureInfo.InvariantCulture);
    }

    public SettingsPageViewModel()
    {
        PageName = ApplicationPageNames.Settings;
    }

    public void AddKspZip(string path)
    {
        AddZipError = null;
        var settings = App.Settings;
        var name = Path.GetFileName(path);

        if (settings.KspZips.Any(z => z.Path == path))
        {
            AddZipError = $"{name} is already in the list.";
            return;
        }

        var zip = _zipService?.GetVersionFromKspZip(path);
        if (zip is null || string.IsNullOrEmpty(zip.Version))
        {
            AddZipError = $"Couldn't read a known KSP version from {name}. Is it a KSP zip?";
            return;
        }

        settings.KspZips.Add(zip);
        SettingsService.Save(settings);
        LoadZips();
    }

    [RelayCommand]
    private void RemoveKspZip(KspZipRow row)
    {
        AddZipError = null;
        var settings = App.Settings;
        settings.KspZips.Remove(row.Zip);
        SettingsService.Save(settings);
        LoadZips();
    }

    partial void OnParallelDownloadsChanged(string value)
    {
        if (App.Settings is not { } settings) return;

        var downloads = int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
            ? Math.Clamp(number, 1, AppSettings.MaxAllowedParallelDownloads)
            : settings.MaxParallelDownloads;

        if (downloads != settings.MaxParallelDownloads)
        {
            settings.MaxParallelDownloads = downloads;
            SettingsService.Save(settings);
        }

        var text = downloads.ToString(CultureInfo.InvariantCulture);
        if (value != text) ParallelDownloads = text;
    }

    private void LoadZips()
    {
        KspZips =
        [
            .. App.Settings.KspZips
                .OrderByDescending(z => z.Version, Comparer<string>.Create(CkanVersion.Compare))
                .ThenBy(z => z.Path, StringComparer.Ordinal)
                .Select(z => new KspZipRow(z))
        ];
    }
}
