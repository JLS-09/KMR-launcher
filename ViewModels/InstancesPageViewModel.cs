using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.Input;
using KMRLauncherMvvm.Data;
using KMRLauncherMvvm.Models;
using KMRLauncherMvvm.Services;
using KMRLauncherMvvm.Views;

namespace KMRLauncherMvvm.ViewModels;

public partial class InstancesPageViewModel : PageViewModel
{
    private ZipService ZipService { get; }

    public Instance? PrimaryInstance
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            OnPropertyChanged();
        }
    }
    
    public bool HasZeroOrOneInstance
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            OnPropertyChanged();
        }
    } = false;
    
    public string? InstancesCount
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            OnPropertyChanged();
        }
    }

    public ObservableCollection<InstanceTile>? InstancesWithoutFirstRow
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            OnPropertyChanged();
        }
    }
    
    public Instance? SecondInstance
    {
        get;
        private set
        {
            if (field == value) return;
            field = value;
            OnPropertyChanged();
        }
    }

    private AppSettings? AppSettings
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            OnPropertyChanged();
        }
    }

    public InstancesPageViewModel(ZipService zipService)
    {
        PageName = ApplicationPageNames.Instances;
        ZipService = zipService;
        Rebuild(App.Settings);
        SettingsService.SettingsChanged += OnSettingsChanged;   
    }

    public InstancesPageViewModel()
    {
        
    }

    private void Rebuild(AppSettings settings)
    {
        AppSettings = settings;
        PrimaryInstance = AppSettings.Instances.FirstOrDefault(i => i.IsPrimary) ?? AppSettings.Instances.FirstOrDefault();
        
        HasZeroOrOneInstance = AppSettings.Instances.Count <= 1;
        InstancesCount = AppSettings.Instances.Count.ToString();
        
        if (PrimaryInstance is null) return;
        
        InstancesWithoutFirstRow =  new ObservableCollection<InstanceTile>(AppSettings.Instances.Where(i => i.RootPath != PrimaryInstance.RootPath));

        if (InstancesWithoutFirstRow.Count == 0)
        {
            return;
        }
        
        SecondInstance = (Instance) InstancesWithoutFirstRow.First();
        InstancesWithoutFirstRow.RemoveAt(0);
        
        InstancesWithoutFirstRow.Add(NewInstancePlaceholder.Instance);
    }

    [RelayCommand]
    private void OpenNewInstanceWindow()
    {
        var window = new NewInstanceWindow
        {
            DataContext = new NewInstanceViewModel(ZipService)
        };
        window.Show();
    }
    
    private void OnSettingsChanged(object? sender, AppSettings settings) => Rebuild(settings);
    
    public void Dispose() => SettingsService.SettingsChanged -= OnSettingsChanged;
}