using System;
using System.IO;
using System.Linq;
using Avalonia.Interactivity;
using CommunityToolkit.Mvvm.Input;
using Humanizer;
using KMRLauncherMvvm.Data;
using KMRLauncherMvvm.Models;
using KMRLauncherMvvm.Services;
using KMRLauncherMvvm.Views;

namespace KMRLauncherMvvm.ViewModels;

public partial class InstancesPageViewModel : PageViewModel
{
    private ZipService ZipService { get; set; }

    public Instance? PrimaryInstance
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
    
    public string? PrimaryInstallSize
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

    public string? PrimaryReadablePlayTime
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
    } = "0s";

    public string PrimaryLastPlayed
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
    } = "Never";

    private AppSettings AppSettings
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

    public InstancesPageViewModel(ZipService zipService)
    {
        PageName = ApplicationPageNames.Instances;
        ZipService = zipService;
        AppSettings = App.Settings;
        PrimaryInstance = AppSettings.Instances.FirstOrDefault(i => i.IsPrimary);
        if (PrimaryInstance is null) return;
        
        PrimaryInstallSize = Helpers.BytesToString(Helpers.GetDirectorySize(PrimaryInstance.RootPath));
        
        if (PrimaryInstance.PlayTime > 0)
        {
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