using CommunityToolkit.Mvvm.ComponentModel;
using KMRLauncherMvvm.Services;
using KMRLauncherMvvm.ViewModels;

namespace KMRLauncherMvvm.Tests;

public partial class DialogStackTests
{
    private sealed partial class SizedDialog : ObservableObject, IDialogSize
    {
        [ObservableProperty] [NotifyPropertyChangedFor(nameof(DialogHeight))]
        private bool _tall;

        public double DialogWidth => 1000;
        public double DialogHeight => Tall ? 1100 : 860;
    }

    [Fact]
    public void ShowRefusesASecondTopLevelDialog()
    {
        var dialogs = new DialogService();
        dialogs.Show("install");

        Assert.Throws<InvalidOperationException>(() => dialogs.Show("other"));
    }

    [Fact]
    public void ShowOverStacksAndCloseRemovesOnlyThatDialog()
    {
        var dialogs = new DialogService();
        var changes = 0;
        dialogs.Changed += () => changes++;

        dialogs.Show("install");
        dialogs.ShowOver("new instance");
        Assert.Equal(["install", "new instance"], dialogs.Open);
        Assert.Equal("new instance", dialogs.Current);

        dialogs.Close("new instance");
        Assert.Equal("install", dialogs.Current);
        dialogs.Close("not open");
        dialogs.Close("install");

        Assert.False(dialogs.IsOpen);
        Assert.Equal(4, changes);
    }

    [Fact]
    public void HostTakesTheDialogsSizeAndFollowsIt()
    {
        var dialog = new SizedDialog();
        var host = new DialogHost(dialog);
        var changed = new List<string?>();
        host.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        Assert.Equal((1000.0, 860.0), (host.Width, host.Height));
        dialog.Tall = true;
        Assert.Equal(1100, host.Height);
        Assert.Contains(nameof(DialogHost.Height), changed);

        host.Detach();
        changed.Clear();
        dialog.Tall = false;
        Assert.Empty(changed);
    }

    [Fact]
    public void HostDefaultsToTheInstallDialogSize()
    {
        var host = new DialogHost("plain");

        Assert.Equal((1080.0, 860.0), (host.Width, host.Height));
    }
}
