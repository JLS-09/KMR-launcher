using System;
using System.IO;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using System.Net.Http;
using Avalonia.Markup.Xaml;
using KMRLauncherMvvm.Data;
using KMRLauncherMvvm.Factories;
using KMRLauncherMvvm.Models;
using KMRLauncherMvvm.Services;
using KMRLauncherMvvm.Services.Api;
using KMRLauncherMvvm.ViewModels;
using KMRLauncherMvvm.Views;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace KMRLauncherMvvm;

public class App : Application
{
    
    public static AppSettings Settings { get; set; }
    
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        
        this.AttachDeveloperTools();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        InitializeSettings();
        
        var modListService = new ModListService();
        var jsonHelper = new GitHelper(modListService);
        var compatibilityService = new CompatibilityService(modListService);
        
        var collection = new ServiceCollection();
        collection.AddSingleton<IModApiService, ModGitService>();
        collection.AddSingleton<ZipService>();
        collection.AddSingleton(modListService);
        collection.AddSingleton(jsonHelper);
        collection.AddSingleton(compatibilityService);
        
        collection.AddTransient<MainWindowViewModel>();
        collection.AddTransient<HomePageViewModel>();
        collection.AddTransient<DiscoverPageViewModel>();
        collection.AddTransient<InstancesPageViewModel>();
        collection.AddTransient<SettingsPageViewModel>();

        collection.AddSingleton<Func<ApplicationPageNames, PageViewModel>>(x => name => name switch
        {
            ApplicationPageNames.Home => x.GetRequiredService<HomePageViewModel>(),
            ApplicationPageNames.Discover => x.GetRequiredService<DiscoverPageViewModel>(),
            ApplicationPageNames.Instances => x.GetRequiredService<InstancesPageViewModel>(),
            ApplicationPageNames.Settings => x.GetRequiredService<SettingsPageViewModel>(),
            _ => throw new InvalidOperationException()
        });
        
        collection.AddSingleton<PageFactory>();
        
        var services = collection.BuildServiceProvider();
        
        var vm = services.GetRequiredService<MainWindowViewModel>();
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = vm
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    public static void InitializeSettings()
    {
        var basePath = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") 
                       ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
        var appFolder = Path.Combine(basePath, "kmrLauncher");

        Directory.CreateDirectory(appFolder);
        
        var settingsFile = Path.Combine(appFolder, "settings.json");
        
        if (!File.Exists(settingsFile))
            SettingsService.Save(new AppSettings());
        
        Settings = SettingsService.Load();
    }
}