using System;
using System.IO;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
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
    
    public static AppSettings Settings { get; private set; }
    
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        InitializeSettings();
        
        var collection = new ServiceCollection();
        collection.AddSingleton<IModApiService, ModApiService>();
        collection.AddSingleton<ZipService>();
        
        var config = new ConfigurationBuilder()
            .AddUserSecrets<App>()
            .Build();
        
        collection.AddSingleton(new HttpClient
        {
            BaseAddress = new Uri(config["ApiBaseAddress"] ?? "http://localhost:3000/")
        });
        
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
            DisableAvaloniaDataAnnotationValidation();
            desktop.MainWindow = new MainWindow
            {
                DataContext = vm
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void InitializeSettings()
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

    private void DisableAvaloniaDataAnnotationValidation()
    {
        // Get an array of plugins to remove
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        // remove each entry found
        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}