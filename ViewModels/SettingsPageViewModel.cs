using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Input;
using KMRLauncherMvvm.Data;

namespace KMRLauncherMvvm.ViewModels;

public partial class SettingsPageViewModel : PageViewModel
{
    public string SelectedFolderPath
    {
        get;
        set { if (field != value) { field = value; OnPropertyChanged(); }}
    }

    public SettingsPageViewModel()
    {
        SelectedFolderPath = App.Settings.KspZipLocation;
        PageName = ApplicationPageNames.Settings;
    }
    
    [RelayCommand]
    private async Task BrowseFolder(UserControl view)
    {
        var topLevel = TopLevel.GetTopLevel(view);
        if (topLevel is null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select File",
            AllowMultiple = false,
            FileTypeFilter = new[] { FilePickerFileTypes.All } // or restrict by type
        });

        if (files.Count > 0)
        {
            SelectedFolderPath = files[0].Path.LocalPath;
        }
    }
}