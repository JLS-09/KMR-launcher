using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using KMRLauncherMvvm.ViewModels;

namespace KMRLauncherMvvm.Views;

public partial class SettingsPageView : UserControl
{
    private const double SettingsTwoColumnWidth = 830;

    private readonly (Button Nav, Control Section)[] _sections;
    private Control? _pinned;
    private double _pinnedOffset;

    public SettingsPageView()
    {
        InitializeComponent();
        _sections = [(KspNav, KspSection), (DownloadsNav, DownloadsSection)];
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        Classes.Set("stacked", e.NewSize.Width < SettingsTwoColumnWidth);
    }

    private void SectionNav_OnClick(object? sender, RoutedEventArgs e)
    {
        var target = _sections.FirstOrDefault(s => s.Nav == sender).Section;
        if (target is null) return;

        _pinned = target;
        _pinnedOffset = Math.Min(SectionTop(target), MaxOffset());
        SetActive(target);
        Scroller.Offset = new Vector(Scroller.Offset.X, _pinnedOffset);
    }

    private void Scroller_OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (_pinned is not null && Math.Abs(Scroller.Offset.Y - _pinnedOffset) < 1) return;
        _pinned = null;

        var offset = Scroller.Offset.Y;
        var max = MaxOffset();
        var active = max > 0 && offset >= max - 1
            ? _sections[^1].Section
            : _sections.LastOrDefault(s => SectionTop(s.Section) <= offset + 1).Section ?? _sections[0].Section;
        SetActive(active);
    }

    private double SectionTop(Control section) =>
        section == _sections[0].Section
            ? 0
            : Math.Max(0, (section.TranslatePoint(default, SectionsHost)?.Y ?? 0) - section.Margin.Top);

    private double MaxOffset() => Math.Max(0, Scroller.Extent.Height - Scroller.Viewport.Height);

    private void SetActive(Control section)
    {
        foreach (var (nav, s) in _sections) nav.Classes.Set("active", s == section);
    }

    private async void AddKspZip_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not SettingsPageViewModel vm) return;
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage) return;

        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Add a KSP zip",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Zip files") { Patterns = ["*.zip"] }, FilePickerFileTypes.All]
        });
        if (files.FirstOrDefault()?.TryGetLocalPath() is { } path) vm.AddKspZip(path);
    }
}
