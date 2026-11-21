using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace KMRLauncherMvvm.ViewModels.Settings;

public partial class AccentSwatch(string name, string hex) : ObservableObject
{
    public string Name { get; } = name;
    public string Hex { get; } = hex;
    public IBrush Brush { get; } = new SolidColorBrush(Color.Parse(hex));

    [ObservableProperty] private bool _isSelected;
}
