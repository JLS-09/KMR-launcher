using System;
using System.IO;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using KMRLauncherMvvm.Data;
using KMRLauncherMvvm.Factories;
using KMRLauncherMvvm.Models;
using KMRLauncherMvvm.Services;
using KMRLauncherMvvm.ViewModels.NewInstance;
using KMRLauncherMvvm.Services.Api;
using KMRLauncherMvvm.ViewModels;
using KMRLauncherMvvm.ViewModels.InstallMods;
using KMRLauncherMvvm.Views;
using Microsoft.Extensions.DependencyInjection;

namespace KMRLauncherMvvm;

public class App : Application
{

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        
        this.AttachDeveloperTools();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var instances = new InstanceService(SettingsService.DefaultFile);
        ThemeService.ApplyAccent(instances.Settings.AccentColor);
        
        var modListService = new ModListService();
        var jsonHelper = new GitHelper(modListService);
        var compatibilityService = new CompatibilityService(modListService);
        var dependencyService = new DependencyService(modListService, compatibilityService);
        
        var collection = new ServiceCollection();
        collection.AddSingleton(instances);
        collection.AddSingleton<SyncStatusService>();
        collection.AddSingleton<StatusBarViewModel>();
        collection.AddSingleton<GameSessionService>();
        collection.AddSingleton<NavigationService>();
        collection.AddSingleton<DialogService>();
        collection.AddSingleton<InstallFlow>();
        collection.AddSingleton<IModApiService, ModGitService>();
        collection.AddSingleton<ZipService>();
        collection.AddSingleton(sp => new InstanceCreator(sp.GetRequiredService<ZipService>(),
            (_, instance) => instances.Save(instance, modsChanged: true)));
        collection.AddSingleton<NewInstanceFlow>();
        collection.AddSingleton(modListService);
        collection.AddSingleton(jsonHelper);
        collection.AddSingleton(compatibilityService);
        collection.AddSingleton(dependencyService);
        var downloader = new ModDownloader(ModDownloader.CreateHttpClient(), ModDownloader.DefaultCacheFolder);
        collection.AddSingleton<IModDownloader>(downloader);
        collection.AddSingleton(new DownloadCache(ModDownloader.DefaultCacheFolder, downloader.CachePath));
        collection.AddSingleton(new InstallService(downloader, instance => instances.Save(instance, modsChanged: true)));
        
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
}