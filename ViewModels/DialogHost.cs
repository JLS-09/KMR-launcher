using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using KMRLauncherMvvm.Services;

namespace KMRLauncherMvvm.ViewModels;

public partial class DialogHost : ObservableObject
{
    public object Content { get; }
    public double Width => (Content as IDialogSize)?.DialogWidth ?? 1080;
    public double Height => (Content as IDialogSize)?.DialogHeight ?? 860;

    public DialogHost(object content)
    {
        Content = content;
        if (content is INotifyPropertyChanged notify) notify.PropertyChanged += OnContentChanged;
    }

    public void Detach()
    {
        if (Content is INotifyPropertyChanged notify) notify.PropertyChanged -= OnContentChanged;
    }

    private void OnContentChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IDialogSize.DialogWidth)) OnPropertyChanged(nameof(Width));
        if (e.PropertyName == nameof(IDialogSize.DialogHeight)) OnPropertyChanged(nameof(Height));
    }
}
