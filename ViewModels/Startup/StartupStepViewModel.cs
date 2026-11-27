using CommunityToolkit.Mvvm.ComponentModel;

namespace KMRLauncherMvvm.ViewModels.Startup;

public enum StartupStepState
{
    Pending,
    Current,
    Done,
    Skipped,
    Failed
}

public partial class StartupStepViewModel(string title) : ObservableObject
{
    public string Title { get; } = title;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPending), nameof(IsCurrent), nameof(IsDone), nameof(IsSkipped),
        nameof(IsFailed))]
    private StartupStepState _state;

    [ObservableProperty] private string _detail = "";

    public bool IsPending => State == StartupStepState.Pending;
    public bool IsCurrent => State == StartupStepState.Current;
    public bool IsDone => State == StartupStepState.Done;
    public bool IsSkipped => State == StartupStepState.Skipped;
    public bool IsFailed => State == StartupStepState.Failed;

    public void Start(string detail = "")
    {
        State = StartupStepState.Current;
        Detail = detail;
    }

    public void Finish(string detail, StartupStepState state = StartupStepState.Done)
    {
        State = state;
        Detail = detail;
    }
}
