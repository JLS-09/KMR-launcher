using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Input;
using KMRLauncherMvvm.Data;
using KMRLauncherMvvm.Models;
using KMRLauncherMvvm.Services;

namespace KMRLauncherMvvm.ViewModels;

public partial class SettingsPageViewModel : PageViewModel
{
    public IReadOnlyList<string> AllKspVersions => KspVersions.All;
    private ZipService ZipService { get; set; }
    private string ZipRelativeRootPath { get; set; }

    public AppSettings AppSettings
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged();
            }
        }
    }

    public string ZipPath
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged();
            }
        }
    }

    public string ZipVersion
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged();
            }
        }
    }

    public SettingsPageViewModel(ZipService zipService)
    {
        PageName = ApplicationPageNames.Settings;
        AppSettings = App.Settings;
        ZipService = zipService;
    }

    [RelayCommand]
    private async Task BrowseFolder(UserControl view)
    {
        var topLevel = TopLevel.GetTopLevel(view);
        if (topLevel is null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select KSP zip",
            AllowMultiple = false,
            FileTypeFilter = [FilePickerFileTypes.All]
        });

        if (files.Count > 0)
        {
            var kspZip = ZipService.GetVersionFromKspZip(files[0].Path.LocalPath);
            ZipPath = kspZip.Path;
            ZipRelativeRootPath = kspZip.RelativeRootPath;
            ZipVersion = kspZip.Version;
        }
    }

    [RelayCommand]
    private void AddKspZip()
    {
        if (string.IsNullOrWhiteSpace(ZipPath) ||
            string.IsNullOrWhiteSpace(ZipRelativeRootPath) ||
            string.IsNullOrWhiteSpace(ZipVersion)) return;

        AppSettings.KspZips.Add(new KspZip(ZipPath, ZipRelativeRootPath, ZipVersion));
        SettingsService.Save(AppSettings);

        ZipPath = string.Empty;
        ZipVersion = string.Empty;
    }

    [RelayCommand]
    private void RemoveKspZip(KspZip kspZip)
    {
        AppSettings.KspZips.Remove(kspZip);
        SettingsService.Save(AppSettings);
    }
}