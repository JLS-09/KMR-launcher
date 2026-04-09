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
    public string KspZipPath
    {
        get;
        set { if (field != value) { field = value; OnPropertyChanged(); }}
    }
    
    public string KspZipVersion
    {
        get;
        set { if (field != value) { field = value; OnPropertyChanged(); } }
    }
    
    public AppSettings AppSettings
    {
        get;
        set { if (field != value) { field = value; OnPropertyChanged(); } }
    }

    public SettingsPageViewModel()
    {
        PageName = ApplicationPageNames.Settings;
        AppSettings = App.Settings;
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
            KspZipPath = files[0].Path.LocalPath;
        }
    }

    [RelayCommand]
    private void AddKspZip()
    {
        if (string.IsNullOrWhiteSpace(KspZipPath) || string.IsNullOrWhiteSpace(KspZipVersion)) return;
        
        AppSettings.KspZips.Add(new KspZip(KspZipPath, KspZipVersion));
        SettingsService.Save(AppSettings);
        
        KspZipPath = string.Empty;
        KspZipVersion = string.Empty;
    }
}