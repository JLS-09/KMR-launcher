using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
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
using KMRLauncherMvvm.ViewModels.Startup;
using KMRLauncherMvvm.Views;
using Microsoft.Extensions.DependencyInjection;

namespace KMRLauncherMvvm;

public class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

#if DEBUG
        this.AttachDeveloperTools();
#endif
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
        collection.AddSingleton<HomeDataService>();
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
        collection.AddSingleton(new InstallService(downloader,
            instance => instances.Save(instance, modsChanged: true)));

        collection.AddTransient<StartupViewModel>();
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

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var startup = services.GetRequiredService<StartupViewModel>();
            var window = new StartupWindow { DataContext = startup };
            desktop.ShutdownMode = ShutdownMode.OnLastWindowClose;
            desktop.MainWindow = window;
            window.Opened += async (_, _) => await OpenMainWindowAsync(desktop, window, startup, services);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static async Task OpenMainWindowAsync(IClassicDesktopStyleApplicationLifetime desktop,
        StartupWindow window, StartupViewModel startup, IServiceProvider services)
    {
        var closed = false;
        window.Closed += (_, _) => closed = true;

        try
        {
            await startup.RunAsync();
        }
        catch (Exception)
        {
        }

        if (closed) return;

        var main = new MainWindow { DataContext = services.GetRequiredService<MainWindowViewModel>() };
        desktop.MainWindow = main;
        main.Show();
        window.Close();
    }
}