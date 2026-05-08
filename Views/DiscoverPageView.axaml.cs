using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using KMRLauncherMvvm.ViewModels;

namespace KMRLauncherMvvm.Views;

public partial class DiscoverPageView : UserControl
{
    public DiscoverPageView()
    {
        InitializeComponent();
    }
    
    private async void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        try
        {
            if (sender is not ScrollViewer scrollViewer) return;
            if (DataContext is not DiscoverPageViewModel vm) return;

            var offset = scrollViewer.Offset;
            var extent = scrollViewer.Extent;
            var viewport = scrollViewer.Viewport;

            const int threshold = 50;
            var isNearBottom = offset.Y >= extent.Height - viewport.Height - threshold;

            if (isNearBottom)
            {
                await vm.FetchMoreMods();
            }
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
        }
    }
}