using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KMRLauncherMvvm.Models;
using KMRLauncherMvvm.Services;

namespace KMRLauncherMvvm.ViewModels;

public partial class NewInstanceViewModel : PageViewModel
{
    private ZipService ZipService { get; set; }
    
    [ObservableProperty]
    private double _extractionProgress;
    
    [ObservableProperty]
    private string _extractionProgressHumanized;

    [ObservableProperty]
    private bool _isExtracting;
    
    [ObservableProperty]
    private string? _currentFile;
    
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
            Title = "Select Folder",
            AllowMultiple = false
        });

        if (files.Count > 0)
        {
            InstancePath = files[0].Path.LocalPath;
        }
    }

    [RelayCommand]
    private void CloseWindow(Window window)
    {
        window.Close();
    }
    
    [RelayCommand]
    private async Task AddNewInstance(Window window)
    {
        try
        {
            IsExtracting = true;
            ExtractionProgress = 0;

            var progress = new Progress<ExtractionProgress>(p =>
            {
                ExtractionProgress = p.Percentage;
                ExtractionProgressHumanized = $"{Math.Round(p.Percentage * 100, 0)}%";
                CurrentFile = p.CurrentFile;
            });
            
            var fullInstancePath = Path.Combine(InstancePath, Name);
            if (AppSettings.Instances.Any(i => i.Name == Name && i.RootPath == fullInstancePath))
            {
                throw new Exception("Cannot create new instance with the same name in the same directory");
            }
            
            Directory.CreateDirectory(fullInstancePath);
            await ZipService.ExtractFolderFromZip(SelectedZip.Path, SelectedZip.RelativeRootPath, fullInstancePath, progress);
            AppSettings.Instances.Add(new Instance(Name, fullInstancePath, SelectedZip.Version));
            SettingsService.Save(AppSettings);
            CloseWindow(window);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
        finally
        {
            IsExtracting = false;
        }
    }
}