using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace KMRLauncherMvvm.Controls;

public class LaunchButton : TemplatedControl
{
    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<LaunchButton, ICommand?>(nameof(Command));

    public static readonly StyledProperty<object?> CommandParameterProperty =
        AvaloniaProperty.Register<LaunchButton, object?>(nameof(CommandParameter));

    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<LaunchButton, string>(nameof(Text), "Launch");

    public static readonly StyledProperty<MenuFlyout?> MenuProperty =
        AvaloniaProperty.Register<LaunchButton, MenuFlyout?>(nameof(Menu));

    public ICommand? Command
    {
        get => GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public MenuFlyout? Menu
    {
        get => GetValue(MenuProperty);
        set => SetValue(MenuProperty, value);
    }
}
