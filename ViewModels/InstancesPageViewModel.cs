using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.Input;
using Humanizer;
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
    
    public string? PrimaryInstallSize
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            OnPropertyChanged();
        }
    }

    public string? PrimaryReadablePlayTime
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            OnPropertyChanged();
        }
    } = "0s";

    public string PrimaryLastPlayed
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            OnPropertyChanged();
        }
    } = "Never";
    
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

        if (InstancesWithoutFirstRow.Count > 0)
        {
            SecondInstance = InstancesWithoutFirstRow.First();
            InstancesWithoutFirstRow.RemoveAt(0);
        }
        
        PrimaryInstallSize = Helpers.BytesToString(Helpers.GetDirectorySize(PrimaryInstance.RootPath));

        if (PrimaryInstance.PlayTime <= 0) return;
        
        PrimaryLastPlayed = PrimaryInstance.LastPlayed.Humanize()
            .Replace("year", "yr")
            .Replace("month", "mon")
            .Replace("day", "d")
            .Replace("hour", "hr")
            .Replace("minute", "min")
            .Replace("second", "sec");
        
        PrimaryReadablePlayTime = new TimeSpan(0, 0, PrimaryInstance.PlayTime)
            .Humanize(precision: 2, maxUnit: TimeUnit.Hour)
            .Replace(" hours", "h")
            .Replace(" minutes", "m")
            .Replace(" seconds", "s")
            .Replace(" hour", "h")
            .Replace(" minute", "m")
            .Replace(" second", "s");
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