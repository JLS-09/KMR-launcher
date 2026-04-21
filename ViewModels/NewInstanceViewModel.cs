using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Input;
using KMRLauncherMvvm.Models;
using KMRLauncherMvvm.Services;

namespace KMRLauncherMvvm.ViewModels;

public partial class NewInstanceViewModel : PageViewModel
{
    private ZipService ZipService { get; set; }
    
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
    
    public KspZip SelectedZip
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
    
    public string Name
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
    
    public string InstancePath
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
    
    public NewInstanceViewModel(ZipService zipService)
    {
        AppSettings = App.Settings;
        ZipService = zipService;
    }

    public NewInstanceViewModel()
    {
        
    }
    
    [RelayCommand]
    private async Task BrowseFolder(Window view)
    {
        var topLevel = TopLevel.GetTopLevel(view);
        if (topLevel is null) return;

        var files = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select KSP zip",
            AllowMultiple = false
        });

        if (files.Count > 0)
        {
            InstancePath = files[0].Path.LocalPath;
        }
    }

    [RelayCommand]
    private async Task AddNewInstance()
    {
        try
        {
            
            var fullInstancePath = Path.Combine(InstancePath, Name);
            if (AppSettings.Instances.Any(i => i.Name == Name && i.RootPath == fullInstancePath))
            {
                throw new Exception("Cannot create new instance with the same name in the same directory");
            }
            
            Directory.CreateDirectory(fullInstancePath);
            await ZipService.ExtractFolderFromZip(SelectedZip.Path, SelectedZip.RelativeRootPath, fullInstancePath);
            AppSettings.Instances.Add(new Instance(Name, fullInstancePath));
            SettingsService.Save(AppSettings);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }
}