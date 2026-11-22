using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Layout;
using KMRLauncherMvvm.Controls;
using KMRLauncherMvvm.ViewModels.InstallMods;

namespace KMRLauncherMvvm.Views.InstallMods;

public partial class InstallDialogView : UserControl
{
    private InstallDialogViewModel? _viewModel;

    public InstallDialogView()
    {
        InitializeComponent();
        Focusable = true;
    }

    protected override void OnDataContextChanged(System.EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_viewModel is not null) _viewModel.PropertyChanged -= OnViewModelChanged;
        _viewModel = DataContext as InstallDialogViewModel;
        if (_viewModel is not null) _viewModel.PropertyChanged += OnViewModelChanged;
        BuildStepper();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Focus();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key != Key.Escape || _viewModel is not { ShowClose: true }) return;

        _viewModel.CloseCommand.Execute(null);
        e.Handled = true;
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(InstallDialogViewModel.Steps)) BuildStepper();
    }

    private void BuildStepper()
    {
        Stepper.Children.Clear();
        Stepper.ColumnDefinitions.Clear();
        if (_viewModel is null) return;

        var column = 0;
        foreach (var step in _viewModel.Steps)
        {
            Stepper.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            var item = StepItem(step);
            Grid.SetColumn(item, column++);
            Stepper.Children.Add(item);

            if (!step.HasLineAfter) continue;

            Stepper.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            var line = new Border { Classes = { "stepLine" } };
            line.Classes.Set("reached", step.LineReached);
            Grid.SetColumn(line, column++);
            Stepper.Children.Add(line);
        }
    }

    private static Control StepItem(StepperItem step)
    {
        var dot = new Border { Classes = { "stepDot" } };
        dot.Classes.Set("current", step.IsCurrent);
        dot.Classes.Set("done", step.IsDone);
        if (step.IsDone)
        {
            var check = new StrokeIcon
            {
                Data = (Avalonia.Media.Geometry?)Application.Current?.FindResource("IconCheck"),
                Size = 12,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            check.Bind(StrokeIcon.ForegroundProperty, check.GetResourceObservable("AccentText"));
            dot.Child = check;
        }
        else
        {
            dot.Child = new TextBlock { Text = step.Number.ToString() };
        }

        var label = new TextBlock { Text = step.Label, Classes = { "stepLabel" } };
        label.Classes.Set("current", step.IsCurrent);
        label.Classes.Set("done", step.IsDone);

        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children = { dot, label }
        };
    }

    private async void CopyError_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is null || TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard) return;
        await clipboard.SetTextAsync(_viewModel.Outcome.ErrorText);
    }
}
