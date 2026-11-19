using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace KMRLauncherMvvm.Controls;

public enum ModStatusKind
{
    Fits,
    Installed,
    Update,
    Warning,
    Error
}

// Icon + short text in the status colour: "Fits", "Installed", "Update", "Not 1.12.5".
// The look per kind is in Styles/Controls.axaml (:fits, :installed, :update, :warning, :error).
public class StatusLabel : TemplatedControl
{
    public static readonly StyledProperty<ModStatusKind> KindProperty =
        AvaloniaProperty.Register<StatusLabel, ModStatusKind>(nameof(Kind));

    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<StatusLabel, string?>(nameof(Text));

    public StatusLabel() => UpdatePseudoClasses();

    public ModStatusKind Kind
    {
        get => GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == KindProperty) UpdatePseudoClasses();
    }

    private void UpdatePseudoClasses()
    {
        PseudoClasses.Set(":fits", Kind == ModStatusKind.Fits);
        PseudoClasses.Set(":installed", Kind == ModStatusKind.Installed);
        PseudoClasses.Set(":update", Kind == ModStatusKind.Update);
        PseudoClasses.Set(":warning", Kind == ModStatusKind.Warning);
        PseudoClasses.Set(":error", Kind == ModStatusKind.Error);
    }
}
