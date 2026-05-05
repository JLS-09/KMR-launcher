using System;
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
    
    public string InstancesCount
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            OnPropertyChanged();
        }
    }

    public ObservableCollection<Instance> InstancesWithoutFirstRow
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            OnPropertyChanged();
        }
    }
    
    public Instance SecondInstance
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            OnPropertyChanged();
        }
    }

    private AppSettings AppSettings
    {
        get;
        init
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
        AppSettings = App.Settings;
        PrimaryInstance = AppSettings.Instances.FirstOrDefault(i => i.IsPrimary) ?? AppSettings.Instances.FirstOrDefault();
        
        HasZeroOrOneInstance = AppSettings.Instances.Count <= 1;
        
        InstancesCount = AppSettings.Instances.Count.ToString();
        
        if (PrimaryInstance is null) return;
        
        InstancesWithoutFirstRow =  new ObservableCollection<Instance>(AppSettings.Instances.Where(i => i.RootPath != PrimaryInstance.RootPath));

        if (InstancesWithoutFirstRow.Count <= 0) return;
        
        SecondInstance = InstancesWithoutFirstRow.First();
        InstancesWithoutFirstRow.RemoveAt(0);
        
        Console.WriteLine(InstancesWithoutFirstRow.Count);
    }

    public InstancesPageViewModel()
    {
        
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
}