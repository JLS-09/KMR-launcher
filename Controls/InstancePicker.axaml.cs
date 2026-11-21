using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using KMRLauncherMvvm.Models;

namespace KMRLauncherMvvm.Controls;

public partial class InstancePicker : UserControl
{
    public static readonly StyledProperty<IEnumerable<Instance>?> InstancesProperty =
        AvaloniaProperty.Register<InstancePicker, IEnumerable<Instance>?>(nameof(Instances));

    public static readonly StyledProperty<Instance?> SelectedInstanceProperty =
        AvaloniaProperty.Register<InstancePicker, Instance?>(nameof(SelectedInstance),
            defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<bool> IsCompactProperty =
        AvaloniaProperty.Register<InstancePicker, bool>(nameof(IsCompact));

    public InstancePicker()
    {
        InitializeComponent();
        PickerButton.DataContext = this;
        InstanceList.DataContext = this;
    }

    public IEnumerable<Instance>? Instances
    {
        get => GetValue(InstancesProperty);
        set => SetValue(InstancesProperty, value);
    }

    public Instance? SelectedInstance
    {
        get => GetValue(SelectedInstanceProperty);
        set => SetValue(SelectedInstanceProperty, value);
    }

    public bool IsCompact
    {
        get => GetValue(IsCompactProperty);
        set => SetValue(IsCompactProperty, value);
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e) => PickerButton.Flyout?.Hide();
}
